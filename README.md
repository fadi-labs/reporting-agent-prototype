# reporting-agent-prototype

An AI reporting agent for personal stock portfolio analytics.

It listens to a user prompt, calls MCP tools, discovers the right fields, and turns the request into SQL against Druid to pull back the answer. It runs through LiteLLM, so you can plug in Azure AI Foundry, OpenAI, or Anthropic by swapping env vars. In practice, it can:

- translate messy business questions into precise Druid queries
- inspect available fields and tags before querying
- fetch live reporting data from the seeded `Reporting` table
- surface results as a clean, conversational answer

Think of it as a prompt-to-SQL analyst with tool access.

## Example prompts

```
Show me a count of stocks grouped by their position status. Which status has the most holdings?
```

```
List 10 stocks with the highest gain percentage. Show ticker, current price, and gain percentage.
```

The agent will discover the right fields, build the SQL, run it against Druid, and explain the results.

## Field retrieval modes

The MCP server has two modes for resolving fields from user prompts:

- **Taxonomy** (default) — deterministic JSON tag filter, no external dependency. Works out of the box.
- **Vector** — semantic search via Qdrant embeddings. Not tested yet. Requires running `reporting.seeder` and setting the mode in `src/reporting.mcp.server/appsettings.json`:

```json
"FieldRetrieval": {
  "Mode": "Vector"
}
```

You also need Azure OpenAI credentials for the embeddings — see SETUP.md § 6.

## Start here

For local setup and run steps, see **[SETUP.md](./SETUP.md)**.
