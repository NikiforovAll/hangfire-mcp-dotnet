namespace Nall.Hangfire.Mcp.Maintenance;

public static class JobFilterMatcher
{
    public static bool Matches(JobMatch match, JobFilter filter)
    {
        if (
            filter.Queue is { } q
            && !string.Equals(match.Queue, q, StringComparison.OrdinalIgnoreCase)
        )
        {
            return false;
        }

        var job = match.Job;
        if (filter.JobType is { } t)
        {
            var typeName = job?.Type?.FullName ?? job?.Type?.Name;
            if (typeName is null || typeName.IndexOf(t, StringComparison.OrdinalIgnoreCase) < 0)
            {
                return false;
            }
        }

        if (filter.Method is { } m)
        {
            var methodName = job?.Method?.Name;
            if (methodName is null || methodName.IndexOf(m, StringComparison.OrdinalIgnoreCase) < 0)
            {
                return false;
            }
        }

        if (filter.MessageContains is { } msg)
        {
            if (!ContainsCi(match.Reason, msg) && !ContainsCi(match.ExceptionMessage, msg))
            {
                return false;
            }
        }

        if (filter.ExceptionContains is { } ex)
        {
            if (!ContainsCi(match.ExceptionType, ex) && !ContainsCi(match.ExceptionMessage, ex))
            {
                return false;
            }
        }

        if (filter.Since is { } since && (match.At is null || match.At < since))
        {
            return false;
        }

        if (filter.Until is { } until && (match.At is null || match.At > until))
        {
            return false;
        }

        return true;
    }

    private static bool ContainsCi(string? haystack, string needle) =>
        haystack is not null && haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
}
