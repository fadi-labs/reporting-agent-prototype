namespace reproting.chatagent;

public static class ExamplePrompts
{
    public static readonly IReadOnlyList<(string Title, string Prompt)> All =
    [
        (
            "Customer orders by status",
            "Show me a count of customer orders grouped by their status. Which status has the most orders?"
        ),
        (
            "Customer orders in ACCEPTED status",
            "List 10 customer orders that are currently in ACCEPTED status. Show order ID and creation date."
        )
    ];
}

