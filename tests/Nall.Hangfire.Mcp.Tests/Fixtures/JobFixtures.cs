namespace Nall.Hangfire.Mcp.Tests.Fixtures;

public interface IEmailJob
{
    Task SendAsync(string to);

    Task SendAsync(string to, string subject);
}

public class ReportJob
{
    public Task GenerateAsync(int year, string format = "pdf") => Task.CompletedTask;

    public Task<int> CountRowsAsync() => Task.FromResult(0);
}

public class NullableJob
{
    public Task RunAsync(int required, int? optionalValue, string? optionalRef) =>
        Task.CompletedTask;

    public Task RefOnlyAsync(string requiredRef, string? optionalRef) => Task.CompletedTask;
}
