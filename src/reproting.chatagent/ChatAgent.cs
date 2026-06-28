using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;

namespace reproting.chatagent;

/// <summary>
/// Stateful chat agent that routes user questions to the MCP server tools,
/// runs the agentic tool-calling loop, and returns a final answer.
/// </summary>
public sealed class ChatAgent
{
    private readonly IChatClient _chatClient;
    private readonly IList<McpClientTool> _tools;
    private readonly List<ChatMessage> _history;
    private readonly ChatOptions _options;

    private const string SystemPrompt = """
        You are a supply chain data analyst assistant. You help users query shipment and logistics
        data stored in Apache Druid using SQL.

        When the user asks a data question, follow this exact workflow:

        Step 1 — Discover field categories:
          Call get_field_tags with the most relevant universe name.
          Available universes: "Customer Order", "Shipper Booking", "Carrier Booking",
          "Cargo Stuffing", "Shipping Instruction", "Events And Milestones", "Destination"

        Step 2 — Retrieve column definitions:
          Call get_fields with relevant tags and the universe(s) to get column IDs,
          data types, descriptions, and allowed values.

        Step 3 — Build and run a SQL query:
          Construct a valid SQL SELECT against the "Reporting" table using column IDs.
          Call query_shipment_data to execute it and get results.

        Step 4 — Present results:
          Show the data clearly, include row counts, and summarise key insights.

        SQL rules (the server enforces these):
        - Always use column IDs (e.g. customerOrder_orderStatus), never display names.
        - The table is always "Reporting" (capital R).
        - SELECT specific columns only — never SELECT *.
        - No CTEs (WITH …), no UNION.
        - Results are capped at 500 rows; use GROUP BY + aggregates for summaries.
        - Use COUNT(*) for counts, GROUP BY for breakdowns.
        """;

    public ChatAgent(IChatClient chatClient, IList<McpClientTool> tools)
    {
        _chatClient = chatClient;
        _tools = tools;
        _options = new ChatOptions { Tools = [.. tools] };
        _history = [new ChatMessage(ChatRole.System, SystemPrompt)];
    }

    /// <summary>
    /// Sends a user message and runs the agentic loop until the LLM produces a final answer.
    /// Conversation history is retained across calls.
    /// </summary>
    public async Task<string> SendMessageAsync(string userMessage, CancellationToken ct = default)
    {
        _history.Add(new ChatMessage(ChatRole.User, userMessage));

        while (true)
        {
            var response = await _chatClient.GetResponseAsync(_history, _options, ct);

            // Append the new response messages to our history
            foreach (var msg in response.Messages)
                _history.Add(msg);

            // Collect all tool calls from this round
            var toolCalls = response.Messages
                .SelectMany(m => m.Contents.OfType<FunctionCallContent>())
                .ToList();

            // No tool calls → the LLM produced its final text answer
            if (toolCalls.Count == 0)
                return response.Text ?? string.Empty;

            // Execute each tool call via the MCP server and feed results back
            foreach (var call in toolCalls)
            {
                WriteToolCallLine(call.Name, call.Arguments);

                var tool = _tools.FirstOrDefault(t => t.Name == call.Name);
                if (tool is null)
                {
                    _history.Add(new ChatMessage(ChatRole.Tool,
                        [new FunctionResultContent(call.CallId, "Tool not found.")]));
                    continue;
                }

                try
                {
                    var args = call.Arguments is not null
                        ? new AIFunctionArguments(call.Arguments)
                        : null;

                    var result = await tool.InvokeAsync(args, ct);
                    _history.Add(new ChatMessage(ChatRole.Tool,
                        [new FunctionResultContent(call.CallId, result)]));
                }
                catch (Exception ex)
                {
                    _history.Add(new ChatMessage(ChatRole.Tool,
                        [new FunctionResultContent(call.CallId, $"Error: {ex.Message}")]));
                }
            }
        }
    }

    /// <summary>Clears conversation history, keeping only the system prompt.</summary>
    public void Reset()
    {
        _history.Clear();
        _history.Add(new ChatMessage(ChatRole.System, SystemPrompt));
    }

    private static void WriteToolCallLine(string toolName, IDictionary<string, object?>? args)
    {
        var argSummary = args is null
            ? ""
            : string.Join(", ", args.Select(kv => $"{kv.Key}={kv.Value}"));

        var original = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.WriteLine($"  [tool] {toolName}({argSummary})");
        Console.ForegroundColor = original;
    }
}

