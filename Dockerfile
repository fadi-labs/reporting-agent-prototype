FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY global.json Directory.Build.props reporting-agent-prototype.sln ./
COPY src/reporting.agent.core/reporting.agent.core.csproj src/reporting.agent.core/
COPY src/reporting.mcp.server/reporting.mcp.server.csproj src/reporting.mcp.server/
COPY tests/reporting.agent.tests/reporting.agent.tests.csproj tests/reporting.agent.tests/
RUN dotnet restore src/reporting.mcp.server/reporting.mcp.server.csproj

COPY src/ src/

RUN dotnet publish src/reporting.mcp.server/reporting.mcp.server.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./

ENV ASPNETCORE_URLS=http://0.0.0.0:8001 \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_PRINT_TELEMETRY_MESSAGE=false

EXPOSE 8001

HEALTHCHECK --interval=30s --timeout=3s --retries=3 \
  CMD curl -fsS http://localhost:8001/healthz || exit 1

ENTRYPOINT ["dotnet", "reporting.mcp.server.dll"]

