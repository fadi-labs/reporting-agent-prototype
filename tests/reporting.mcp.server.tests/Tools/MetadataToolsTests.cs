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

    // Criterion 1: empty universes
    [Fact]
    public async Task GetFields_EmptyUniverses_ThrowsArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            Build().GetFields([], ["status"]));
    }

    // Criterion 2: empty tags
    [Fact]
    public async Task GetFields_EmptyTags_ThrowsArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            Build().GetFields(["Customer Order"], []));
    }

    // Criterion 3: unrecognised universe names the value and lists valid universes
    [Fact]
    public async Task GetFields_UnrecognisedUniverse_ThrowsWithUniverseNameAndValidList()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            Build().GetFields(["Foo"], ["status"]));
        Assert.Contains("Foo", ex.Message);
        Assert.Contains("Customer Order", ex.Message);
    }

    // Criterion 4: unrecognised tag names the value and lists valid tags
    [Fact]
    public async Task GetFields_UnrecognisedTag_ThrowsWithTagNameAndValidList()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            Build().GetFields(["Customer Order"], ["banana"]));
        Assert.Contains("banana", ex.Message);
        Assert.Contains("identifier", ex.Message);
    }

    // Criterion 5: top_k = 0
    [Fact]
    public async Task GetFields_TopKZero_ThrowsArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            Build().GetFields(["Customer Order"], ["status"], top_k: 0));
    }

    // Criterion 6: top_k = 101
    [Fact]
    public async Task GetFields_TopK101_ThrowsArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            Build().GetFields(["Customer Order"], ["status"], top_k: 101));
    }

    // Criterion 7: both invalid universe and tag — single exception reports both violations
    [Fact]
    public async Task GetFields_InvalidUniverseAndTag_SingleExceptionReportsBothViolations()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            Build().GetFields(["Foo"], ["banana"]));
        Assert.Contains("Foo", ex.Message);
        Assert.Contains("banana", ex.Message);
    }

    // Criterion 8: lowercase universe succeeds (case-insensitive matching)
    [Fact]
    public async Task GetFields_LowercaseUniverse_Succeeds()
    {
        _retriever.RetrieveAsync(
                Arg.Any<IReadOnlyList<string>>(),
                Arg.Any<IReadOnlyList<string>>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .Returns(Array.Empty<FieldResult>());

        await Build().GetFields(["customer order"], ["status"]);

        await _retriever.Received(1).RetrieveAsync(
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Any<int>(),
            Arg.Any<CancellationToken>());
    }
}
