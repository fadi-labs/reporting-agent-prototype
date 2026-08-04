# CLAUDE.md — reporting.mcp.server

## MCP Tool Conventions

- Tool classes must be annotated with `[McpServerToolType]` and registered as `sealed`.
- Each tool method gets `[McpServerTool(Name = "snake_case_name")]` plus a `[Description(...)]` attribute — the description is the only documentation an LLM sees, so make it precise and include usage order hints (e.g. "call this FIRST").
- Parameter descriptions are set with `[Description(...)]` on each argument. Always document valid enum-like values inline (universes, tags).
- Always include a `CancellationToken ct = default` as the last parameter.
- `column_id` values returned by `get_fields` must never be renamed or invented — they are the authoritative identifiers consumed by `query_shipment_data`.
- Log every tool entry and exit with structured logging (`_logger.LogInformation`) using the tool name and key parameters.
