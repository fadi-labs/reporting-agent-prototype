-- Provisions databases used by Druid (metadata) and the .NET MCP server
-- (entity tools query the `staging` DB; `reporting` is reserved for parity
-- with the Python repo's DB_NAME_REPORT).

CREATE DATABASE druid;
CREATE DATABASE staging;
CREATE DATABASE reporting;
