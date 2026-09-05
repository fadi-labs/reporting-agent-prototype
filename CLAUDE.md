# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What This Project Is

A supply chain data analytics agent that uses AI to translate natural-language business questions into Apache Druid SQL queries. It exposes tools via the Model Context Protocol (MCP) and supports multiple LLM providers through LiteLLM.

## Tech Stack

- **.NET 10** (see `global.json`)
- **Apache Druid** — analytics query backend
- **Qdrant** — optional vector store for semantic field discovery
- **PostgreSQL** — metadata storage
- **LiteLLM** — LLM proxy (port 4000)
- **ModelContextProtocol 1.4.0** — tool framework for MCP server

## Build & Run

```powershell
# Build core library
dotnet build src/reporting.agent.core/reporting.agent.core.csproj -c Release

# Start Docker services (Druid, ZooKeeper, Postgres, Qdrant)
docker compose up -d

# Ingest sample data into Druid
curl.exe -X POST -H "Content-Type: application/json" `
  --data-binary "@docker/druid-sample/ingest-spec.json" `
  http://localhost:8081/druid/indexer/v1/task

# Start the MCP server (port 8001)
dotnet run --project src\reporting.mcp.server

# Start LiteLLM proxy (port 4000) — pick any supported provider/model
litellm --host 0.0.0.0 --port 4000 --model <provider/model>

# Run the chat agent
dotnet run --project src\reproting.chatagent

# (Optional) Seed Qdrant with field embeddings for vector mode
dotnet run --project src\reporting.seeder
```

Run unit tests:
```powershell
dotnet test tests/reporting.mcp.server.tests/reporting.mcp.server.tests.csproj
```

Run smoke tests (requires running MCP server):
```powershell
.\scripts\smoke-test.ps1
```

## Architecture

Four projects in `src/`:

| Project | Role |
|---|---|
| `reporting.agent.core` | Core library — Druid client, SQL validation/transformation, field retrieval, embeddings |
| `reporting.mcp.server` | MCP server exposing tools to LLMs |
| `reproting.chatagent` | Console chat agent using OpenAI SDK + MCP client (note: typo in directory name) |
| `reporting.seeder` | Seeds Qdrant with field embeddings |

### MCP Tools (in `reporting.mcp.server/Tools/`)

- `get_field_tags` — lists available field category tags for a universe
- `get_fields` — returns column definitions with metadata for a universe
- `query_stock_data` — validates and executes SQL against Druid
- `execute_report` / `download_report` — run saved reports via Secure Gateway

### Field Retrieval Modes

Toggled via `FieldRetrieval:Mode` in appsettings:
- **Taxonomy** — deterministic; reads column definition JSONs from `reporting.mcp.server/Resources/columns/` per universe
- **Vector** — semantic; queries Qdrant using Azure OpenAI embeddings

### SQL Pipeline

User SQL flows through `reporting.agent.core/Services/Sql/`:
1. **Validation** — rejects CTEs, UNION, `SELECT *`, enforces 500-row max
2. **Transformation** — auto-injects deduplication and soft-delete filters

Key constraint: SQL must use **column IDs** (e.g., `stockTicker`, `sharesOwned`), not display names. Date filtering requires `TIME_PARSE()`, not `CAST() AS TIMESTAMP`.

### Universes

Stocks.

## Configuration

Each project has its own `appsettings.json`; environment variables override. Key settings:
- `Druid:Host` — Druid broker URL (PostgreSQL is only used as Druid's metadata store)
- `Qdrant:Url` — vector store (only needed in Vector mode)
- `AzureOpenAI:*` — embeddings endpoint and key
- `FieldRetrieval:Mode` — `Taxonomy` or `Vector`

## Testing

When implementing a feature that has a spec in `specs/`, work RED-GREEN against each acceptance criterion. See `.claude/rules/testing.md` — this rule is always on.

## Architecture decisions

Documented in `docs/adr/` — read before changing anything the ADRs cover. Rules for when a new ADR is required: `.claude/rules/architecture.md`.

## Skills

- `/specify <feature>` — interviews you to produce a spec in `specs/<feature>.md` before any code is written. See `.claude/skills/specify/SKILL.md`. Template: `specs/_template.md`.

## CI/CD

- `.github/workflows/ci.yml` — builds `reporting.agent.core` on push/PR to `master`
- `.github/workflows/pipeline-code-review-report.yml` — AI-assisted code review workflow
