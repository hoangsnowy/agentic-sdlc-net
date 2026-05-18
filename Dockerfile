# Multi-stage Dockerfile cho AgenticSdlc.Api (.NET 10, Phase 6).
# Build: docker build -t agentic-sdlc-net:latest .
# Run:   docker run -p 8080:8080 -e Llm__Anthropic__ApiKey=sk-... agentic-sdlc-net:latest

# ---- Stage 1: build ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj + restore (cache layer)
COPY global.json Directory.Build.props ./
COPY AgenticSdlc.sln ./
COPY src/AgenticSdlc.Domain/AgenticSdlc.Domain.csproj            src/AgenticSdlc.Domain/
COPY src/AgenticSdlc.Application/AgenticSdlc.Application.csproj  src/AgenticSdlc.Application/
COPY src/AgenticSdlc.Infrastructure/AgenticSdlc.Infrastructure.csproj src/AgenticSdlc.Infrastructure/
COPY src/AgenticSdlc.Api/AgenticSdlc.Api.csproj                  src/AgenticSdlc.Api/
COPY tests/AgenticSdlc.Tests/AgenticSdlc.Tests.csproj            tests/AgenticSdlc.Tests/
RUN dotnet restore AgenticSdlc.sln

# Copy source + publish Release
COPY . .
RUN dotnet publish src/AgenticSdlc.Api/AgenticSdlc.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

# ---- Stage 2: runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Non-root user (security best practice — App Container chạy uid 1654 mặc định)
RUN groupadd --gid 1654 app && useradd --uid 1654 --gid app --no-create-home app

COPY --from=build /app/publish .
RUN chown -R app:app /app
USER app

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_USE_POLLING_FILE_WATCHER=false

EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
    CMD wget --quiet --tries=1 --spider http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "AgenticSdlc.Api.dll"]
