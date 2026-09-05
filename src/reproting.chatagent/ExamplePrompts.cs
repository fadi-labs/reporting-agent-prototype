namespace reproting.chatagent;

public static class ExamplePrompts
{
    public static readonly IReadOnlyList<(string Title, string Prompt)> All =
    [
        (
            "Stocks by position status",
            "Show me a count of stocks grouped by their position status. Which status has the most holdings?"
        ),
        (
            "Stocks with largest gain",
            "List 10 stocks with the highest gain percentage. Show ticker, current price, and gain percentage."
        )
    ];
}
