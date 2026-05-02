var builder = DistributedApplication.CreateBuilder(args);

var pgUser = builder.AddParameter("pg-username");
var pgPassword = builder.AddParameter("pg-password", secret: true);

var postgresServer = builder
    .AddPostgres("postgres-server", pgUser, pgPassword, 5432)
    .WithImageTag("15.7")
    .WithDataVolume()
    .WithLifetime(ContainerLifetime.Persistent);

var postgresDatabase = postgresServer
    .AddDatabase("hangfire")
    .WithCreationScript(
        """
        CREATE DATABASE hangfire;
        """
    );

#pragma warning disable ASPIREPOSTGRES001
postgresDatabase.WithPostgresMcp();
#pragma warning restore ASPIREPOSTGRES001

var keycloak = builder
    .AddKeycloakContainer("keycloak")
    .WithImageTag("26.4")
    .WithImport("./KeycloakConfiguration/HangfireMcp-realm.json")
    .WithImport("./KeycloakConfiguration/HangfireMcp-users-0.json");

var realm = keycloak.AddRealm("HangfireMcp");

#pragma warning disable ASPIREMCP001
var web = builder
    .AddProject<Projects.Web>("server")
    .WithReference(postgresDatabase)
    .WaitFor(postgresDatabase)
    .WithReference(keycloak)
    .WithReference(realm)
    .WaitFor(keycloak)
    .WithMcpServer("/mcp");
#pragma warning restore ASPIREMCP001

builder
    .AddMcpInspector("inspector", new McpInspectorOptions { InspectorVersion = "0.21.2" })
    .WithEnvironment("DANGEROUSLY_OMIT_AUTH", "true")
    .WithEnvironment("NODE_OPTIONS", "--max-http-header-size=32768")
    .WithMcpServer(web);

builder.Build().Run();
