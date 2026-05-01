using System.Text.Json;
using Nall.Hangfire.Mcp.Tests.Fixtures;
using Shouldly;

namespace Nall.Hangfire.Mcp.Tests;

public class JobArgumentBinderTests
{
    private static IReadOnlyDictionary<string, JsonElement> Args(string json) =>
        JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;

    [Fact]
    public void Binds_required_and_optional_parameters()
    {
        var method = typeof(ReportJob).GetMethod(nameof(ReportJob.GenerateAsync))!;

        var bound = JobArgumentBinder.Bind(method, Args("""{"year": 2026, "format": "csv"}"""));

        bound.ShouldBe([2026, "csv"]);
    }

    [Fact]
    public void Falls_back_to_default_value_when_argument_missing()
    {
        var method = typeof(ReportJob).GetMethod(nameof(ReportJob.GenerateAsync))!;

        var bound = JobArgumentBinder.Bind(method, Args("""{"year": 2026}"""));

        bound.ShouldBe([2026, "pdf"]);
    }

    [Fact]
    public void Throws_when_required_argument_missing()
    {
        var method = typeof(ReportJob).GetMethod(nameof(ReportJob.GenerateAsync))!;

        Should.Throw<ArgumentException>(() => JobArgumentBinder.Bind(method, Args("{}")));
    }

    [Fact]
    public void Accepts_null_arguments_dictionary_when_all_have_defaults()
    {
        var method = typeof(ReportJob).GetMethod(nameof(ReportJob.CountRowsAsync))!;

        var bound = JobArgumentBinder.Bind(method, arguments: null);

        bound.ShouldBeEmpty();
    }

    [Fact]
    public void Binds_omitted_nullable_parameters_to_null()
    {
        var method = typeof(NullableJob).GetMethod(nameof(NullableJob.RunAsync))!;

        var bound = JobArgumentBinder.Bind(method, Args("""{"required": 1}"""));

        bound.ShouldBe([1, null, null]);
    }

    [Fact]
    public void Throws_when_non_nullable_required_missing_even_if_others_nullable()
    {
        var method = typeof(NullableJob).GetMethod(nameof(NullableJob.RunAsync))!;

        Should.Throw<ArgumentException>(() => JobArgumentBinder.Bind(method, Args("{}")));
    }

    [Fact]
    public void Throws_on_null_method()
    {
        Should.Throw<ArgumentNullException>(() => JobArgumentBinder.Bind(null!, arguments: null));
    }
}
