using System.Text.Json;
using Nall.Hangfire.Mcp.Tests.Fixtures;
using Shouldly;

namespace Nall.Hangfire.Mcp.Tests;

public class JobInputSchemaTests
{
    [Fact]
    public void Build_emits_object_schema_with_properties_and_required()
    {
        var method = typeof(ReportJob).GetMethod(nameof(ReportJob.GenerateAsync))!;

        var schema = JobInputSchema.Build(method);
        var json = schema.GetRawText();

        var doc = JsonDocument.Parse(json).RootElement;
        doc.GetProperty("type").GetString().ShouldBe("object");

        var props = doc.GetProperty("properties");
        props.TryGetProperty("year", out _).ShouldBeTrue();
        props.TryGetProperty("format", out _).ShouldBeTrue();

        var required = doc.GetProperty("required")
            .EnumerateArray()
            .Select(e => e.GetString())
            .ToArray();
        required.ShouldBe(["year"]);
    }

    [Fact]
    public void Build_omits_required_when_all_parameters_have_defaults()
    {
        var method = typeof(ReportJob).GetMethod(nameof(ReportJob.CountRowsAsync))!;

        var schema = JobInputSchema.Build(method);
        var doc = JsonDocument.Parse(schema.GetRawText()).RootElement;

        doc.TryGetProperty("required", out _).ShouldBeFalse();
        doc.GetProperty("properties").EnumerateObject().ShouldBeEmpty();
    }

    [Fact]
    public void Build_treats_nullable_value_and_reference_types_as_optional()
    {
        var method = typeof(NullableJob).GetMethod(nameof(NullableJob.RunAsync))!;

        var schema = JobInputSchema.Build(method);
        var doc = JsonDocument.Parse(schema.GetRawText()).RootElement;

        var required = doc.GetProperty("required")
            .EnumerateArray()
            .Select(e => e.GetString())
            .ToArray();
        required.ShouldBe(["required"]);
    }

    [Fact]
    public void Build_keeps_non_nullable_reference_required()
    {
        var method = typeof(NullableJob).GetMethod(nameof(NullableJob.RefOnlyAsync))!;

        var schema = JobInputSchema.Build(method);
        var doc = JsonDocument.Parse(schema.GetRawText()).RootElement;

        var required = doc.GetProperty("required")
            .EnumerateArray()
            .Select(e => e.GetString())
            .ToArray();
        required.ShouldBe(["requiredRef"]);
    }

    [Fact]
    public void Build_throws_on_null_method()
    {
        Should.Throw<ArgumentNullException>(() => JobInputSchema.Build(null!));
    }

    [Fact]
    public void Build_emits_parameter_description_from_direct_attribute()
    {
        var method = typeof(DirectlyDescribedJob).GetMethod(nameof(DirectlyDescribedJob.RunAsync))!;

        var schema = JobInputSchema.Build(method);
        var doc = JsonDocument.Parse(schema.GetRawText()).RootElement;

        doc.GetProperty("properties")
            .GetProperty("value")
            .GetProperty("description")
            .GetString()
            .ShouldBe("Direct param description.");
    }

    [Fact]
    public void Build_emits_parameter_description_from_interface_fallback()
    {
        var method = typeof(DescribedJob).GetMethod(nameof(DescribedJob.RunAsync))!;

        var schema = JobInputSchema.Build(method);
        var doc = JsonDocument.Parse(schema.GetRawText()).RootElement;

        var props = doc.GetProperty("properties");
        props
            .GetProperty("name")
            .GetProperty("description")
            .GetString()
            .ShouldBe("Iface-level param description.");
        props.GetProperty("count").TryGetProperty("description", out _).ShouldBeFalse();
    }

    [Fact]
    public void Build_omits_description_when_no_attribute()
    {
        var method = typeof(UndescribedJob).GetMethod(nameof(UndescribedJob.RunAsync))!;

        var schema = JobInputSchema.Build(method);
        var doc = JsonDocument.Parse(schema.GetRawText()).RootElement;

        doc.GetProperty("properties")
            .GetProperty("value")
            .TryGetProperty("description", out _)
            .ShouldBeFalse();
    }
}
