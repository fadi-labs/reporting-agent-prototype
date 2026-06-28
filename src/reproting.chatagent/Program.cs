using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using ModelContextProtocol.Client;
using OpenAI;
using System.ClientModel;
using reproting.chatagent;

// ── Configuration ──────────────────────────────────────────────────────────────
var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .AddEnvironmentVariables()
    .Build();

var mcpEndpoint = config["McpServer:Endpoint"] ?? "http://localhost:8001/";
var baseUrl     = config["LiteLLM:BaseUrl"]    ?? "";
var model       = config["LiteLLM:Model"]      ?? "";
var apiKey      = config["LiteLLM:ApiKey"]     ?? "litellm";

if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(model))
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.Error.WriteLine("ERROR: Set LiteLLM:BaseUrl and LiteLLM:Model in appsettings.json");
    Console.Error.WriteLine("       or via environment variables LiteLLM__BaseUrl / LiteLLM__Model.");
    Console.Error.WriteLine("       See SETUP.md § 4 for examples.");
    Console.ResetColor();
    return 1;
}

if (string.IsNullOrWhiteSpace(apiKey))
    apiKey = "litellm";

var chatClient = new OpenAI.Chat.ChatClient(
        model: model,
        credential: new ApiKeyCredential(apiKey),
        options: new OpenAIClientOptions { Endpoint = NormalizeOpenAiBaseUrl(baseUrl) })
    .AsIChatClient();

// ── Banner ─────────────────────────────────────────────────────────────────────
Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine();
Console.WriteLine("  reporting-agent-prototype — Chat Agent");
Console.WriteLine("  ================================");
Console.ResetColor();
Console.WriteLine($"  LLM  : {model} via LiteLLM ({baseUrl})");
Console.WriteLine($"  MCP  : {mcpEndpoint}");
Console.WriteLine();

// ── Connect to MCP server (Streamable HTTP transport) ─────────────────────────
Console.Write("Connecting to MCP server... ");
McpClient mcpClient;
try
{
    mcpClient = await McpClient.CreateAsync(
        new HttpClientTransport(new HttpClientTransportOptions
        {
            Endpoint = new Uri(mcpEndpoint)
        }));
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"FAILED\n{ex.Message}");
    Console.WriteLine("\nMake sure reporting.mcp.server is running on port 8001.");
    Console.ResetColor();
    return 1;
}

var mcpTools = await mcpClient.ListToolsAsync();
Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine($"OK  ({mcpTools.Count} tools: {string.Join(", ", mcpTools.Select(t => t.Name))})");
Console.ResetColor();

var agent = new ChatAgent(chatClient, mcpTools);

// ── Show example prompts ───────────────────────────────────────────────────────
Console.WriteLine();
Console.ForegroundColor = ConsoleColor.DarkGray;
Console.WriteLine("  Example prompts (enter a number, or type your own question):");
Console.ResetColor();

for (int i = 0; i < ExamplePrompts.All.Count; i++)
{
    Console.ForegroundColor = ConsoleColor.DarkCyan;
    Console.Write($"    [{i + 1}] ");
    Console.ResetColor();
    Console.WriteLine(ExamplePrompts.All[i].Title);
}

Console.ForegroundColor = ConsoleColor.DarkGray;
Console.WriteLine();
Console.WriteLine("  Commands: 'new' — start a fresh conversation | 'exit' — quit");
Console.ResetColor();

// ── Main chat loop ─────────────────────────────────────────────────────────────
while (true)
{
    Console.WriteLine();
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.Write("You: ");
    Console.ResetColor();

    var input = Console.ReadLine()?.Trim();

    if (string.IsNullOrEmpty(input))
        continue;

    if (input.Equals("exit", StringComparison.OrdinalIgnoreCase))
        break;

    if (input.Equals("new", StringComparison.OrdinalIgnoreCase))
    {
        agent.Reset();
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("  [conversation reset]");
        Console.ResetColor();
        continue;
    }

    if (int.TryParse(input, out var idx) && idx >= 1 && idx <= ExamplePrompts.All.Count)
    {
        var (title, prompt) = ExamplePrompts.All[idx - 1];
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"  [{title}]");
        Console.ResetColor();
        input = prompt;
    }

    try
    {
        var response = await agent.SendMessageAsync(input);

        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.White;
        Console.Write("Assistant: ");
        Console.ResetColor();
        Console.WriteLine(response);
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"\nError: {ex.Message}");
        Console.ResetColor();
    }
}

await mcpClient.DisposeAsync();
return 0;

static Uri NormalizeOpenAiBaseUrl(string baseUrl)
{
    var trimmed = baseUrl.TrimEnd('/');
    if (!trimmed.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
        trimmed += "/v1";

    // Trailing slash required: the OpenAI SDK resolves "chat/completions"
    // as a relative path, and without it .NET URI resolution drops /v1.
    return new Uri(trimmed + "/");
}

