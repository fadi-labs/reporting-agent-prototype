# Spec: `list_universes` MCP tool

## Context

Today an LLM using this MCP server has no in-band way to discover which universes it
may pass to `get_field_tags`, `get_fields`, or `query_shipment_data`. The valid set
lives in `src/reporting.mcp.server/Tools/MetadataTools.cs` as the private
`ValidUniverses` list and is only surfaced piecemeal via the `[Description]` on
individual parameters. LLMs must therefore either be pre-primed with the list or
guess, which risks calling downstream tools with unknown universe names and getting
`ArgumentException`s back.

This spec introduces a new MCP tool, `list_universes`, exposed from
`MetadataTools`, that returns the full set of supported universes with a one-line
description of each. It is intended to be the first call in the discovery funnel:
`list_universes` → `get_field_tags` → `get_fields` → `query_shipment_data`.

Only `src/reporting.mcp.server/Tools/MetadataTools.cs` and its test project are
affected. There are no SQL, Druid, or field-retrieval (Taxonomy / Vector) changes.

## Behaviour

- A new tool method `ListUniverses` on `MetadataTools` is registered with
  `[McpServerTool(Name = "list_universes")]` and a `[Description]` that positions
  it as the entry point of the discovery flow (before `get_field_tags`).
- The tool takes **no parameters** other than the standard
  `CancellationToken ct = default`.
- It returns an ordered list of objects, one per universe. Each object has exactly
  two fields:
  - `name` — the display name, matching the exact strings already used in
    `ValidUniverses` (e.g. `"Customer Order"`, `"Shipper Booking"`).
  - `description` — a single, human-readable sentence describing what the universe
    represents in the supply-chain domain (roughly ≤ 140 characters).
- The list contains **all 8** universes currently in `ValidUniverses`:
  Customer Order, Shipper Booking, Carrier Booking, Cargo Stuffing,
  Shipping Instruction, Events And Milestones, Destination,
  Customer Messaging Service. The order returned matches the order of
  `ValidUniverses` (declaration order).
- Universe metadata (names + descriptions) is defined as a **static readonly
  collection inside `MetadataTools`**, alongside the existing `ValidUniverses`
  list. No new configuration files, no JSON resources, no I/O.
- `ValidUniverses` and the new metadata collection must remain consistent — the
  set of `name` values returned by `list_universes` must equal the set in
  `ValidUniverses` (see acceptance criteria).
- Tool entry and exit are logged with structured logging via
  `_logger.LogInformation`, following the pattern used by `GetFieldTags` /
  `GetFields` (tool name plus the count of universes returned on exit).
- The `[Description]` string on the tool makes the discovery order explicit,
  e.g. *"Call this FIRST to discover which universes are available, then call
  `get_field_tags(universe)` to explore each one."*

## Edge cases

- **Concurrent invocations** — the tool is stateless and reads from a static
  readonly collection; no synchronisation is required, and calls must be safe to
  run in parallel.
- **Cancellation** — `ct` should be honoured. Because the implementation is
  purely in-memory, cancellation will typically not fire, but the parameter must
  still be present as the last argument to match repo conventions
  (`src/reporting.mcp.server/CLAUDE.md`).
- **Drift between `ValidUniverses` and the new metadata** — a future edit that
  adds a universe to `ValidUniverses` without adding a matching description (or
  vice versa) is a bug. A test must fail loudly in that case rather than the
  tool silently returning a partial list.
- **Localisation** — descriptions are English-only for this iteration; no
  culture/locale handling.
- No SQL is generated or executed, so Druid constraints (no CTEs, no `UNION`,
  no `SELECT *`, 500-row cap, `TIME_PARSE`) do not apply.
- Field retrieval mode (`Taxonomy` vs `Vector`) is irrelevant — this tool does
  not touch `IFieldRetriever`.

## Out of scope

- No new configuration surface, JSON resource files, or database reads.
- No changes to `get_field_tags`, `get_fields`, `query_shipment_data`,
  `execute_report`, or `download_report`.
- No changes to `IFieldRetriever`, its Taxonomy or Vector implementations, or
  to the SQL validation / transformation pipeline.
- No field-count, tag-sample, or link-to-column-definitions data in the
  response — only `name` + `description`.
- No filtering or pagination parameters.
- No changes to the chat agent (`src/reproting.chatagent`) — behaviour there is
  driven by the LLM picking up the new tool via MCP.
- No localisation / i18n of descriptions.
- No new ADR is required (this adds a tool that is fully consistent with the
  patterns in `src/reporting.mcp.server/CLAUDE.md` and does not change any
  existing architectural decision).

## Acceptance criteria

- [ ] `MetadataTools` exposes a `ListUniverses` method annotated with
  `[McpServerTool(Name = "list_universes")]` and a `[Description]` that
  explicitly instructs the LLM to call it **first**, before `get_field_tags`.
- [ ] Calling `list_universes` with no arguments returns exactly 8 entries.
- [ ] The returned `name` values, compared as an unordered set, equal
  `ValidUniverses` in `MetadataTools`. A test that adds a universe to
  `ValidUniverses` without adding a description (or vice versa) fails.
- [ ] The returned list is in the same order as `ValidUniverses` (declaration
  order), starting with `"Customer Order"` and ending with
  `"Customer Messaging Service"`.
- [ ] Every entry has a non-empty `description` string of at most 140 characters
  and no line breaks.
- [ ] Each `description` mentions the domain concept the universe represents
  (e.g. the entry for `"Customer Order"` references customer orders; the entry
  for `"Events And Milestones"` references events or milestones). A test
  asserts a keyword per universe so descriptions cannot silently drift into
  placeholders.
- [ ] The tool logs one `Information`-level entry on invocation and one on
  return, the latter including the count of universes returned, matching the
  logging style of `GetFieldTags` and `GetFields`.
- [ ] The tool method signature ends with `CancellationToken ct = default`, per
  `src/reporting.mcp.server/CLAUDE.md`.
- [ ] `dotnet test tests/reporting.mcp.server.tests/reporting.mcp.server.tests.csproj`
  passes, including the new tests covering the criteria above.
