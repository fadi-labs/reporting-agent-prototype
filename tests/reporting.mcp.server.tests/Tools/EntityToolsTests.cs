using Microsoft.Extensions.Logging.Abstractions;
using reporting.mcp.server.Tools;
using Xunit;

namespace reporting.mcp.server.tests.Tools;

public class EntityToolsTests
{
    // EntityTools is constructed with null! for the DB service in tests that throw
    // before reaching any DB call — the ArgumentException guard fires first.
    private static EntityTools Build() =>
        new(null!, NullLogger<EntityTools>.Instance);

    [Fact]
    public async Task GetEntityRelationships_AllParamsNullOrEmpty_ThrowsArgumentException()
    {
        var tools = Build();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            tools.GetEntityRelationships(null, null, null, null, null, null));
    }

    [Fact]
    public async Task GetEntityRelationships_OnlyEmptyStrings_ThrowsArgumentException()
    {
        var tools = Build();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            tools.GetEntityRelationships("", "", "", "", "", ""));
    }

    [Fact]
    public async Task GetEntityIdentificationRules_ContainsSbkPrefix()
    {
        var result = await Build().GetEntityIdentificationRules();
        Assert.Contains("SBK", result);
    }

    [Fact]
    public async Task GetEntityIdentificationRules_ContainsCbkPrefix()
    {
        var result = await Build().GetEntityIdentificationRules();
        Assert.Contains("CBK", result);
    }

    [Fact]
    public async Task GetEntityIdentificationRules_ContainsIso6346ContainerPattern()
    {
        var result = await Build().GetEntityIdentificationRules();
        Assert.Contains("ISO 6346", result);
    }

    [Fact]
    public async Task GetEntityIdentificationRules_DescribesIdentificationPriority()
    {
        var result = await Build().GetEntityIdentificationRules();
        Assert.Contains("Identification priority", result);
    }
}
