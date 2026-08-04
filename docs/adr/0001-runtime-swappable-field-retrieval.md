# ADR 0001 — Runtime-swappable field retrieval via IOptionsMonitor

**Status:** Accepted  
**Date:** 2026-08-04

## Context

The MCP server must expose field metadata to LLMs in two distinct ways:

- **Taxonomy mode** — deterministic lookup from curated JSON column definition files bundled with the server. Fast, reproducible, zero external dependencies.
- **Vector mode** — semantic search via Qdrant embeddings, enabling the LLM to find fields by meaning rather than exact tag. Requires Azure OpenAI and a running Qdrant instance.

During development and in environments without a vector store, Taxonomy mode must work independently. In production, operators need to switch to Vector mode without restarting the server.

Additionally, tag-count metadata (`GetTagsForUniverseAsync`) is a structural property of the column catalogue — not derivable from an embedding space — so it must always come from the Taxonomy backend regardless of the active mode.

## Decision

Both `TaxonomyRetriever` and `QdrantFieldRetriever` are registered as singletons at startup and are always fully instantiated. A `FieldRetrievalRouter` (also a singleton) implements `IFieldRetriever` and resolves the active backend on every call by reading `IOptionsMonitor<FieldRetrievalOptions>`, allowing live config changes to take effect without a restart.

`QdrantFieldRetriever` explicitly delegates `GetTagsForUniverseAsync` to `TaxonomyRetriever` rather than querying Qdrant, because tag counts are metadata about the catalogue, not a property of the vector space.

The active mode is set via `FieldRetrieval:Mode` in configuration (`Taxonomy` | `Vector`). The default is `Taxonomy`.

## Consequences

- Operators can flip modes via an environment variable or config reload — no restart needed.
- Both backends are always wired up; misconfigured Vector credentials surface at the point of first use in Vector mode, not at startup.
- Any new `IFieldRetriever` implementation must be registered alongside the existing two and added to the router's selection logic.
- `GetTagsForUniverseAsync` is intentionally a Taxonomy concern: implementations that don't delegate to it will return incorrect tag counts in Vector mode.
