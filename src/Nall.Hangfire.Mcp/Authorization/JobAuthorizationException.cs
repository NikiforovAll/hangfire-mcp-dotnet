namespace Nall.Hangfire.Mcp.Authorization;

public sealed class JobAuthorizationException : Exception
{
    public JobAuthorizationException(string message)
        : base(message) { }

    public JobAuthorizationException(string message, Exception innerException)
        : base(message, innerException) { }
}
