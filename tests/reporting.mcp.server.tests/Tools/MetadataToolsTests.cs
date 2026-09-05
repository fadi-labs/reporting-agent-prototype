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

    // list_universes — AC: exactly 8 entries
    [Fact]
    public async Task ListUniverses_ReturnsExactly8Entries()
    {
        var result = await Build().ListUniverses();
        Assert.Equal(8, result.Count);
    }

    // list_universes — AC: name set equals ValidUniverses (drift check)
    [Fact]
    public async Task ListUniverses_NamesMatchValidUniverseSet()
    {
        var expected = new HashSet<string>
        {
            "Customer Order", "Shipper Booking", "Carrier Booking", "Cargo Stuffing",
            "Shipping Instruction", "Events And Milestones", "Destination", "Customer Messaging Service"
        };

        var result = await Build().ListUniverses();

        Assert.Equal(expected, result.Select(u => u.Name).ToHashSet());
    }

    // list_universes — AC: Customer Order first, Customer Messaging Service last
    [Fact]
    public async Task ListUniverses_OrderIsCustomerOrderFirstAndCustomerMessagingServiceLast()
    {
        var result = await Build().ListUniverses();
        Assert.Equal("Customer Order", result[0].Name);
        Assert.Equal("Customer Messaging Service", result[^1].Name);
    }

    // list_universes — AC: descriptions non-empty, ≤140 chars, no line breaks
    [Fact]
    public async Task ListUniverses_AllDescriptionsNonEmptyShortAndSingleLine()
    {
        var result = await Build().ListUniverses();
        foreach (var universe in result)
        {
            Assert.False(string.IsNullOrWhiteSpace(universe.Description),
                $"{universe.Name}: description must not be empty");
            Assert.True(universe.Description.Length <= 140,
                $"{universe.Name}: description exceeds 140 chars ({universe.Description.Length})");
            Assert.DoesNotContain('\n', universe.Description);
            Assert.DoesNotContain('\r', universe.Description);
        }
    }

    // list_universes — AC: each description contains a domain keyword
    [Theory]
    [InlineData("Customer Order", "order")]
    [InlineData("Shipper Booking", "booking")]
    [InlineData("Carrier Booking", "booking")]
    [InlineData("Cargo Stuffing", "cargo")]
    [InlineData("Shipping Instruction", "instruction")]
    [InlineData("Events And Milestones", "milestone")]
    [InlineData("Destination", "destination")]
    [InlineData("Customer Messaging Service", "message")]
    public async Task ListUniverses_DescriptionContainsDomainKeyword(string universeName, string keyword)
    {
        var result = await Build().ListUniverses();
        var universe = result.Single(u => u.Name == universeName);
        Assert.Contains(keyword, universe.Description, StringComparison.OrdinalIgnoreCase);
    }

    // list_universes — AC: each returned name is accepted by GetFields (consistency with ValidUniverses)
    [Fact]
    public async Task ListUniverses_EachNameIsAcceptedByGetFields()
    {
        _retriever.RetrieveAsync(
                Arg.Any<IReadOnlyList<string>>(),
                Arg.Any<IReadOnlyList<string>>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .Returns(Array.Empty<FieldResult>());

        var tool = Build();
        var universes = await tool.ListUniverses();
        foreach (var u in universes)
        {
            var ex = await Record.ExceptionAsync(() => tool.GetFields([u.Name], ["status"]));
            Assert.Null(ex);
        }
    }
}
