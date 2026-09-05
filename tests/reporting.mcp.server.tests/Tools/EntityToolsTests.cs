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
    public async Task GetEntityRelationships_EmptyStockTicker_ThrowsArgumentException()
    {
        var tools = Build();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            tools.GetEntityRelationships(""));
    }

    [Fact]
    public async Task GetEntityIdentificationRules_ContainsTickerPattern()
    {
        var result = await Build().GetEntityIdentificationRules();
        Assert.Contains("AAPL", result);
    }

    [Fact]
    public async Task GetEntityIdentificationRules_DescribesIdentificationPriority()
    {
        var result = await Build().GetEntityIdentificationRules();
        Assert.Contains("Identification priority", result);
    }
}
