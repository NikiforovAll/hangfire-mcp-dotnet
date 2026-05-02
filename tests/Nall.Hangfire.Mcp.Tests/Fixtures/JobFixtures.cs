using System.ComponentModel;

namespace Nall.Hangfire.Mcp.Tests.Fixtures;

public interface IEmailJob
{
    [Description("Send a transactional email to the given recipient.")]
    Task SendAsync(string to);

    Task SendAsync(string to, string subject);
}

public class ReportJob
{
    [Description("Generate the annual report.")]
    public Task GenerateAsync(int year, string format = "pdf") => Task.CompletedTask;

    public Task<int> CountRowsAsync() => Task.FromResult(0);
}

public class NullableJob
{
    public Task RunAsync(int required, int? optionalValue, string? optionalRef) =>
        Task.CompletedTask;

    public Task RefOnlyAsync(string requiredRef, string? optionalRef) => Task.CompletedTask;
}

public interface IDescribedJob
{
    [Description("Iface-level method description.")]
    Task RunAsync([Description("Iface-level param description.")] string name, int count);
}

public class DescribedJob : IDescribedJob
{
    public Task RunAsync(string name, int count) => Task.CompletedTask;
}

public class DirectlyDescribedJob
{
    [Description("Direct method description.")]
    public Task RunAsync([Description("Direct param description.")] string value) =>
        Task.CompletedTask;
}

public class UndescribedJob
{
    public Task RunAsync(string value) => Task.CompletedTask;
}
