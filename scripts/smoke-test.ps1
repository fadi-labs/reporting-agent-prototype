#Requires -Version 7.0
<#
.SYNOPSIS
    Smoke-tests the MCP server: metadata tools (steps 5a/5b/5c) and query_stock_data (step 6).
.DESCRIPTION
    Runs against http://localhost:8001 by default. All MCP calls use Invoke-WebRequest
    to avoid the curl.exe double-quote stripping bug present in PowerShell <= 7.2.
.PARAMETER BaseUrl
    Override the server URL (default: http://localhost:8001).
.EXAMPLE
    .\scripts\smoke-test.ps1
    .\scripts\smoke-test.ps1 -BaseUrl http://localhost:8001
#>
param(
    [string]$BaseUrl = 'http://localhost:8001'
)

$ErrorActionPreference = 'Stop'

# ── helpers ──────────────────────────────────────────────────────────────────

function Write-Step([string]$msg) { Write-Host "`n==> $msg" -ForegroundColor Cyan }
function Write-Ok([string]$msg)   { Write-Host "    OK: $msg" -ForegroundColor Green }
function Write-Result([string]$json) {
    try   { $json | ConvertFrom-Json | ConvertTo-Json -Depth 10 }
    catch { $json }
}

function Invoke-Mcp([string]$body) {
    $headers = @{ Accept = 'application/json, text/event-stream' }
    if ($script:session) { $headers['Mcp-Session-Id'] = $script:session }
    $r = Invoke-WebRequest -Method POST $BaseUrl/ `
        -ContentType 'application/json' -Headers $headers -Body $body
    if (-not $script:session -and $r.Headers['Mcp-Session-Id']) {
        $script:session = $r.Headers['Mcp-Session-Id'][0]
    }
    $data = ($r.Content | Select-String 'data: (.+)').Matches.Groups[1].Value
    if (-not $data) { throw "No SSE data line in response: $($r.Content)" }
    $data
}

# ── 0. health check ───────────────────────────────────────────────────────────

Write-Step "Health check ($BaseUrl/healthz)"
$health = Invoke-RestMethod "$BaseUrl/healthz"
if ($health.status -ne 'ok') { throw "Health check failed: $health" }
Write-Ok "status = $($health.status)"

# ── 1. MCP initialize ───────────────────────────────────────────────────────

Write-Step "MCP initialize"
$script:session = $null
Invoke-Mcp '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"smoke","version":"0.0"}}}' | Out-Null
Write-Ok "Session: $script:session"

# ── 2. tools/list ───────────────────────────────────────────────────────────

Write-Step "tools/list"
$toolsJson = Invoke-Mcp '{"jsonrpc":"2.0","id":2,"method":"tools/list","params":{}}'
$tools = ($toolsJson | ConvertFrom-Json).result.tools.name
Write-Ok "Registered tools: $($tools -join ', ')"

# ── 3. get_field_tags (step 5a) ────────────────────────────────────────────

Write-Step "get_field_tags — Stocks universe"
$tagsJson = Invoke-Mcp '{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"get_field_tags","arguments":{"universe":"Stocks"}}}'
$tagsText = ($tagsJson | ConvertFrom-Json).result.content[0].text
Write-Ok "Tags (excerpt):"
Write-Result $tagsText

# ── 4. get_fields (step 5b) ────────────────────────────────────────────────

Write-Step "get_fields — Stocks, tags: identifier + status, top_k=5"
$fieldsJson = Invoke-Mcp '{"jsonrpc":"2.0","id":4,"method":"tools/call","params":{"name":"get_fields","arguments":{"universes":["Stocks"],"tags":["identifier","status"],"top_k":5}}}'
$fieldsText = ($fieldsJson | ConvertFrom-Json).result.content[0].text
$fields = $fieldsText | ConvertFrom-Json
Write-Ok "$($fields.Count) fields returned (matched + dependencies):"
$fields | ForEach-Object { Write-Host "    [$($_.role)] $($_.column_id) — $($_.column_name)" }

# ── 5. REST API (step 5c) ──────────────────────────────────────────────────

Write-Step "REST /api/columns — universe list"
$universes = Invoke-RestMethod "$BaseUrl/api/columns"
Write-Ok "$($universes.Count) universes:"
$universes | ForEach-Object { Write-Host "    $($_.name) ($($_.enabled)/$($_.total) enabled)" }

Write-Step "REST /api/columns/stocks/tags"
$restTags = Invoke-RestMethod "$BaseUrl/api/columns/stocks/tags"
Write-Ok "Top 5 tags: $(($restTags | Get-Member -MemberType NoteProperty | Select-Object -First 5 -ExpandProperty Name) -join ', ')"

# ── 6. query_stock_data (step 6) ────────────────────────────────────────────

Write-Step "query_stock_data — stocks with gain/loss from seeded Druid data"
$sql = "SELECT stockTicker, stockName, sharesOwned, currentPrice, gainLossPercentage FROM Reporting WHERE gainLossPercentage > 0 ORDER BY gainLossPercentage DESC"
$body = [ordered]@{
    jsonrpc = '2.0'
    id      = 5
    method  = 'tools/call'
    params  = @{
        name      = 'query_stock_data'
        arguments = @{ sql = $sql; limit = 10 }
    }
} | ConvertTo-Json -Depth 10 -Compress

$queryJson = Invoke-Mcp $body
$queryText = ($queryJson | ConvertFrom-Json).result.content[0].text
$queryParsed = $queryText | ConvertFrom-Json

# query_stock_data returns {"result":[[headers],[row],...]} on success,
# or [{"error":"..."}] on validation failure
if ($queryParsed.PSObject.Properties['result']) {
    $table    = $queryParsed.result   # Object[] : first element = headers, rest = data rows
    $colNames = @($table[0])
    $dataRows = @($table | Select-Object -Skip 1)
    Write-Ok "$($dataRows.Count) rows returned:"
    $dataRows | ForEach-Object {
        $row = @($_)
        $line = (0..($colNames.Count - 1) | ForEach-Object { "$($colNames[$_])=$($row[$_])" }) -join '  '
        Write-Host "    $line"
    }
} else {
    throw "query_stock_data returned an error: $queryText"
}

Write-Host "`nAll smoke tests passed." -ForegroundColor Green
