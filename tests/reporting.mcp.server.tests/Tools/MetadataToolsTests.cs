using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;
using reporting.agent.core.Models;
using reporting.agent.core.Services.Taxonomy;
using reporting.mcp.server.Tools;

namespace reporting.mcp.server.tests.Tools;

public class MetadataToolsTests
{
    private readonly IFieldRetriever _retriever = Substitute.For<IFieldRetriever>();
    private MetadataTools Build() => new(_retriever, NullLogger<MetadataTools>.Instance);

    [Fact]
    public async Task GetFieldTags_DelegatesToRetriever_WithGivenUniverse()
    {
        var expected = new Dictionary<string, int> { ["status"] = 3, ["date"] = 7 };
        _retriever.GetTagsForUniverseAsync("Customer Order", Arg.Any<CancellationToken>())
            .Returns(expected);

        var result = await Build().GetFieldTags("Customer Order");

        Assert.Equal(expected, result);
        await _retriever.Received(1).GetTagsForUniverseAsync("Customer Order", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetFields_DelegatesToRetriever_WithCorrectUniversesAndTags()
    {
        var universes = new[] { "Customer Order" };
        var tags = new[] { "status", "date" };
        var expected = Array.Empty<FieldResult>();
        _retriever.RetrieveAsync(
                Arg.Is<IReadOnlyList<string>>(u => u.SequenceEqual(universes)),
                Arg.Is<IReadOnlyList<string>>(t => t.SequenceEqual(tags)),
                20,
                Arg.Any<CancellationToken>())
            .Returns(expected);

        var result = await Build().GetFields(universes, tags);

        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task GetFields_DefaultTopK_Is20()
    {
        _retriever.RetrieveAsync(
                Arg.Any<IReadOnlyList<string>>(),
                Arg.Any<IReadOnlyList<string>>(),
                20,
                Arg.Any<CancellationToken>())
            .Returns(Array.Empty<FieldResult>());

        await Build().GetFields(["Customer Order"], ["status"]);

        await _retriever.Received(1).RetrieveAsync(
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Any<IReadOnlyList<string>>(),
            20,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetFields_RespectsExplicitTopK()
    {
        _retriever.RetrieveAsync(
                Arg.Any<IReadOnlyList<string>>(),
                Arg.Any<IReadOnlyList<string>>(),
                5,
                Arg.Any<CancellationToken>())
            .Returns(Array.Empty<FieldResult>());

        await Build().GetFields(["Customer Order"], ["status"], top_k: 5);

        await _retriever.Received(1).RetrieveAsync(
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Any<IReadOnlyList<string>>(),
            5,
            Arg.Any<CancellationToken>());
    }
}
