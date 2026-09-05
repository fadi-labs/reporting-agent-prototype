# Spec: Validate get_fields inputs

## Context

Preventive hardening of the `get_fields` MCP tool in `reporting.mcp.server/Tools/MetadataTools.cs`.
No specific failure has been observed — this closes the gap where the LLM could pass empty arrays,
unknown universe names, unknown tags, or out-of-range `top_k` values that silently produce empty
or misleading results from `IFieldRetriever.RetrieveAsync`.

The `get_field_tags` tool is not in scope.

## Behaviour

Before delegating to `IFieldRetriever.RetrieveAsync`, `GetFields` must collect all constraint
violations and, if any exist, throw an exception that surfaces as an MCP error response. No
partial call to the retriever is made when validation fails.

### universes
- Must contain at least one entry.
- Each value must match (case-insensitively) one of the canonical universe names:
  `Stocks`.
- Unrecognised values are included in the error message.

### tags
- Must contain at least one entry.
- Each value must match (case-insensitively) one of the known tags:
  `identifier`, `status`, `date`, `quantity`, `cost`, `currency`, `reference`, `flag`.
- Unrecognised values are included in the error message.

### top_k
- Must be in the range **1–100** inclusive.

### Error reporting
All violations across all three parameters are collected and reported together in a single
MCP error response. The error message must name each invalid value and (for universes and tags)
list the valid options, so the LLM can self-correct without a follow-up round-trip.

## Edge cases

- Mixed valid/invalid `universes` or `tags` arrays — validation fails; no partial retrieval.
- `universes` or `tags` is an empty array — treated as a missing-input error, not a type error.
- `top_k = 0` — out of range; caught alongside any other violations.
- `top_k > 100` — out of range; caught alongside any other violations.
- Case variants like `"stocks"` or `"STATUS"` — accepted after normalisation; not an error.

## Out of scope

- Validation of `get_field_tags` inputs.
- Validation of any inputs to `query_stock_data`, `execute_report`, or `download_report`.
- Changing field retrieval behaviour (no changes to `IFieldRetriever` implementations or routing — see ADR 0001).
- Adding new universes or tags to the allowlists (allowlists must match the existing tool descriptions exactly).

## Acceptance criteria

- [ ] Calling `get_fields` with an empty `universes` array returns an MCP error response.
- [ ] Calling `get_fields` with an empty `tags` array returns an MCP error response.
- [ ] Calling `get_fields` with an unrecognised universe (e.g. `"Foo"`) returns an MCP error response whose message names `"Foo"` and lists the valid universes.
- [ ] Calling `get_fields` with an unrecognised tag (e.g. `"banana"`) returns an MCP error response whose message names `"banana"` and lists the valid tags.
- [ ] Calling `get_fields` with `top_k = 0` returns an MCP error response.
- [ ] Calling `get_fields` with `top_k = 101` returns an MCP error response.
- [ ] When both an invalid universe and an invalid tag are supplied, a single MCP error response reports both violations.
- [ ] Calling `get_fields` with `universes = ["stocks"]` (lowercase) succeeds — case-insensitive matching is applied.
- [ ] Calling `get_fields` with valid inputs reaches `IFieldRetriever.RetrieveAsync` unchanged.
