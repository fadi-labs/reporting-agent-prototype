# Local setup

This is the shortest reliable runbook for running the repo on your workstation.

## Prerequisites

| Tool | Notes |
|------|-------|
| .NET SDK 10.0.x | `dotnet --version` should report `10.0.x` |
| Docker Desktop or Rancher Desktop | Linux containers with `docker compose v2` |
| `curl.exe` | Used to submit the Druid ingest task |
| Azure OpenAI access | Only needed if you want vector mode |

## 1. Start the local services

```powershell
cd C:\GitHub\reporting-agent-prototype
docker compose up -d
docker compose ps
```

This starts Qdrant, Postgres, ZooKeeper, and the Druid cluster.

Endpoints:

| Service | URL |
|---------|-----|
| Druid web console | http://localhost:8888 |
| Druid SQL | http://localhost:8082/druid/v2/sql |
| Qdrant REST | http://localhost:6333 |
| Qdrant gRPC | localhost:6334 |
| Postgres | localhost:5432 |

## 2. Seed Druid

The sample data is `docker\druid-sample\sample-reporting.json`. The compose file mounts that folder into the Druid containers at `/opt/druid-sample`, and the ingest spec points there.

Submit the ingestion task:

```powershell
curl.exe -X POST `
  -H "Content-Type: application/json" `
  --data-binary "@docker\druid-sample\ingest-spec.json" `
  http://localhost:8081/druid/indexer/v1/task
```

Wait for the task to reach `SUCCESS` in the Druid console. If you wipe the Docker volumes later, rerun this step.

## 3. Run the MCP server

```powershell
dotnet run --project src\reporting.mcp.server
```

The server listens on `http://localhost:8001`.

## 4. Start LiteLLM

The chat agent uses LiteLLM as its only LLM backend. LiteLLM runs as a local proxy on port 4000 and forwards to whichever provider you configure.

Install once:

```powershell
uv tool install 'litellm[proxy]'
```

Then start it for your provider:

**Azure AI Foundry / Azure OpenAI**

```powershell
$env:AZURE_API_BASE = "<api-base-url>"  # e.g. https://<your-resource>.openai.azure.com/
$env:AZURE_API_KEY  = "<your-api-key>"

litellm --host 0.0.0.0 --port 4000 --model azure/gpt-4.1-mini
```

**OpenAI**

```powershell
$env:OPENAI_API_KEY = "<your-api-key>"

litellm --host 0.0.0.0 --port 4000 --model gpt-4o
```

**Anthropic**

```powershell
$env:ANTHROPIC_API_KEY = "<your-api-key>"

litellm --host 0.0.0.0 --port 4000 --model anthropic/claude-3-5-sonnet-20241022
```

## 5. Run the chat agent

Set `LiteLLM__Model` to match exactly what you passed to `--model` above, then run:

```powershell
$env:LiteLLM__Model  = "azure/gpt-4.1-mini"   # or gpt-4o / anthropic/claude-...
$env:LiteLLM__ApiKey = "anything"              # only matters if you secured LiteLLM

dotnet run --project src\reproting.chatagent
```

`LiteLLM__BaseUrl` defaults to `http://localhost:4000` — only set it if you run LiteLLM on a different port.

## 6. Optional: seed Qdrant for vector mode

Only do this if you want `FieldRetrieval__Mode=Vector`. The default mode is taxonomy and does not need Qdrant embeddings.

```powershell
$env:AzureOpenAI__Endpoint = "https://<your-resource>.openai.azure.com/"
$env:AzureOpenAI__ApiKey = "<key>"
$env:AzureOpenAI__EmbeddingsDeployment = "text-embedding-3-small"

dotnet run --project src\reporting.seeder
```

## Quick checks

- `curl.exe http://localhost:8082/status`
- `curl.exe http://localhost:8001/healthz`
- `curl.exe http://localhost:6333/collections`
