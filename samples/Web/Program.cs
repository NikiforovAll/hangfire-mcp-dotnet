using System.Security.Claims;
using Hangfire;
using HangfireJobs;
using Keycloak.AuthServices.Authentication;
using Keycloak.AuthServices.Common;
using Microsoft.OpenApi.Models;
using ModelContextProtocol.AspNetCore.Authentication;
using ModelContextProtocol.Authentication;
using Nall.Hangfire.Mcp;
using Web;

var builder = WebApplication.CreateBuilder(args);
builder.AddHangfireServer();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddTransient<ITimeJob, TimeJob>();
builder.Services.AddTransient<ISendMessageJob, SendMessageJob>();
builder.Services.AddTransient<IReportJob, ReportJob>();
builder.Services.AddTransient<IDataExportJob, DataExportJob>();
builder.Services.AddTransient<INotificationJob, NotificationJob>();
builder.Services.AddTransient<MaintenanceJob>();
builder.Services.AddProblemDetails();

// Opt into BOTH discovery sources: recurring storage + compile-time manifest from
// the source generator. Default is RecurringStorage only.
builder.Services.AddHangfireMcp(o => o.Sources = JobDiscoverySources.All);

// Keycloak-backed JWT bearer auth + MCP protected-resource-metadata challenge.
// The library itself is auth-agnostic — MapHangfireMcp returns IEndpointConventionBuilder,
// so any ASP.NET Core auth scheme can be applied by chaining .RequireAuthorization() below.
var keycloakOptions = builder.Configuration.GetKeycloakOptions<KeycloakAuthenticationOptions>()!;

builder.Services.AddKeycloakWebApiAuthentication(
    builder.Configuration,
    options =>
    {
        options.RequireHttpsMetadata = false;
        options.MetadataAddress = KeycloakConstants.OAuthAuthorizationServerMetadataPath;
    }
);

// Aspire service discovery rewrites the Keycloak URL to a container-internal host,
// so JWTs minted via the browser-facing localhost:8080 fail issuer validation
// (token iss = http://localhost:8080/... vs metadata issuer = service-discovery host).
// Pin Authority/MetadataAddress to the public URL so issuers match.
{
    var publicRealm =
        $"{keycloakOptions.AuthServerUrl!.TrimEnd('/')}/realms/{keycloakOptions.Realm}/";
    builder
        .Services.AddOptions<Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerOptions>(
            Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme
        )
        .Configure(o =>
        {
            o.Authority = publicRealm;
            o.MetadataAddress =
                publicRealm + KeycloakConstants.OAuthAuthorizationServerMetadataPath;
            o.TokenValidationParameters.ValidIssuer = publicRealm.TrimEnd('/');
            o.ConfigurationManager = null;
        });
}

builder
    .Services.AddAuthentication()
    .AddMcp(options =>
    {
        options.ResourceMetadata = new ProtectedResourceMetadata
        {
            Resource = builder.Configuration["McpServerUrl"] ?? "http://localhost:5000/mcp",
            AuthorizationServers = { keycloakOptions.KeycloakUrlRealm },
            ScopesSupported = ["openid", "profile"],
        };
    });

builder.Services.AddAuthorization();

// MCP Inspector (browser) fetches /.well-known/oauth-protected-resource/mcp
// cross-origin from http://localhost:6274 during OAuth discovery.
builder.Services.AddCors(o =>
    o.AddDefaultPolicy(p => p.WithOrigins("http://localhost:6274").AllowAnyHeader())
);

// Swagger UI wired to Keycloak — used to obtain a bearer token for testing /mcp.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.AddSecurityDefinition(
        "oauth2",
        new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.OAuth2,
            Flows = new OpenApiOAuthFlows
            {
                AuthorizationCode = new OpenApiOAuthFlow
                {
                    AuthorizationUrl = new Uri(
                        $"{keycloakOptions.KeycloakUrlRealm}protocol/openid-connect/auth"
                    ),
                    TokenUrl = new Uri(
                        $"{keycloakOptions.KeycloakUrlRealm}protocol/openid-connect/token"
                    ),
                    Scopes = new Dictionary<string, string>
                    {
                        ["openid"] = "OpenID",
                        ["profile"] = "Profile",
                    },
                },
            },
        }
    );
    c.AddSecurityRequirement(
        new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "oauth2",
                    },
                },
                new[] { "openid", "profile" }
            },
        }
    );
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Hangfire MCP Sample", Version = "v1" });
});

var app = builder.Build();
app.UseHttpsRedirection();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.UseSwagger();
app.UseSwaggerUI(o =>
{
    o.SwaggerEndpoint("/swagger/v1/swagger.json", "v1");
    o.RoutePrefix = string.Empty;
    o.OAuthClientId("hangfire-mcp");
    o.OAuthUsePkce();
    o.OAuthScopes("openid", "profile");
});

app.MapHangfireDashboard("/hangfire");

var recurring = app.Services.GetRequiredService<IRecurringJobManager>();

// (1) Parameterless interface job.
recurring.AddOrUpdate<ITimeJob>("time.execute", j => j.ExecuteAsync(), Cron.Minutely);

// (2) Overloaded interface methods — primitive vs. complex argument.
recurring.AddOrUpdate<ISendMessageJob>(
    "send-message.text",
    j => j.ExecuteAsync("hello from recurring"),
    Cron.Hourly
);
recurring.AddOrUpdate<ISendMessageJob>(
    "send-message.envelope",
    j => j.ExecuteAsync(new Message { Subject = "subj", Text = "body" }),
    Cron.Daily
);

// (3) Default args + nullable + multiple primitives.
recurring.AddOrUpdate<IReportJob>(
    "report.generate",
    j => j.GenerateAsync(2026, "pdf", null),
    Cron.Daily
);

// (4) Enum + collection params.
recurring.AddOrUpdate<IDataExportJob>(
    "data.export",
    j => j.ExportAsync(ExportFormat.Csv, new[] { "users", "orders" }),
    Cron.Weekly
);

// (5) Nullable params without C# defaults — `message` and `priority` are optional in
// the MCP schema because their nullability annotations mark them so.
recurring.AddOrUpdate<INotificationJob>(
    "notify.dispatch",
    j => j.NotifyAsync("ops", null, null),
    Cron.Hourly
);

// (6) Concrete (non-interface) job class with default arg.
recurring.AddOrUpdate<MaintenanceJob>(
    "maint.rebuild-indexes",
    j => j.RebuildIndexesAsync("public"),
    Cron.Weekly
);

// (7) One-shot enqueue — picked up by the source generator into the static manifest
// even though it is never registered as recurring. Demonstrates JobDiscoverySources.StaticManifest.
var client = app.Services.GetRequiredService<IBackgroundJobClient>();
client.Enqueue<IReportJob>(j => j.PreviewAsync(2026));
client.Enqueue<MaintenanceJob>(j => j.VacuumAsync("orders", true));

// Manifest-only with optional nullable params — only `subject` is required.
client.Enqueue<INotificationJob>(j => j.BroadcastAsync("startup", null, null));

app.MapGet(
        "/jobs",
        (JobCatalog catalog) =>
            Results.Ok(
                catalog.Jobs.Select(d => new
                {
                    d.RecurringJobId,
                    d.ToolName,
                    DeclaringType = d.DeclaringType.FullName,
                    Method = d.Method.Name,
                    Parameters = d
                        .Method.GetParameters()
                        .Select(p => new
                        {
                            p.Name,
                            Type = p.ParameterType.FullName,
                            HasDefault = p.HasDefaultValue,
                        }),
                })
            )
    )
    .RequireAuthorization();

// Echo the authenticated principal — quick way to verify a token works end-to-end
// before exercising /mcp.
app.MapGet(
        "/auth-info",
        (ClaimsPrincipal user) =>
            Results.Ok(
                new
                {
                    Name = user.Identity?.Name,
                    IsAuthenticated = user.Identity?.IsAuthenticated ?? false,
                    Claims = user.Claims.Select(c => new { c.Type, c.Value }),
                }
            )
    )
    .RequireAuthorization();

app.MapHangfireMcp("/mcp")
    .RequireAuthorization(policy =>
        policy
            .RequireAuthenticatedUser()
            .AddAuthenticationSchemes(McpAuthenticationDefaults.AuthenticationScheme)
    );

app.Run();
