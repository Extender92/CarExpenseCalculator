FROM node:22.22.2-bookworm-slim@sha256:9f6d5975c7dca860947d3915877f85607946403fc55349f39b4bc3688448bb6e AS codex-cli
RUN node --version | grep --fixed-strings "v22.22.2" \
    && npm install --global @openai/codex@0.153.0 \
    && codex --version | grep --fixed-strings "codex-cli 0.153.0"

FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:e1ffd2a92ae84c1291bc1b6887501f8af98e6331e7af6d4c8d37168c5e87a64c AS build
WORKDIR /source
RUN dotnet --version | grep --fixed-strings "10.0.400"

COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/backend/CarExpenseCalculator.CodexExtractor/CarExpenseCalculator.CodexExtractor.csproj src/backend/CarExpenseCalculator.CodexExtractor/
COPY src/backend/CarExpenseCalculator.Core/CarExpenseCalculator.Core.csproj src/backend/CarExpenseCalculator.Core/
COPY src/backend/CarExpenseCalculator.Extraction.Contracts/CarExpenseCalculator.Extraction.Contracts.csproj src/backend/CarExpenseCalculator.Extraction.Contracts/
RUN dotnet restore src/backend/CarExpenseCalculator.CodexExtractor/CarExpenseCalculator.CodexExtractor.csproj

COPY src/backend src/backend
RUN dotnet publish src/backend/CarExpenseCalculator.CodexExtractor/CarExpenseCalculator.CodexExtractor.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0@sha256:a4556ed033fa96f984bb7a8d348851cb2d36b1281dd2420070045f664fbb5f94 AS runtime
USER root
RUN apt-get update \
    && apt-get install --yes --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
COPY --from=codex-cli /usr/local/bin/node /usr/local/bin/node
COPY --from=codex-cli /usr/local/bin/codex /usr/local/bin/codex
COPY --from=codex-cli /usr/local/lib/node_modules /usr/local/lib/node_modules
RUN node --version | grep --fixed-strings "v22.22.2" \
    && codex --version | grep --fixed-strings "codex-cli 0.153.0" \
    && mkdir --parents /var/lib/codex /tmp/car-expense-codex-work \
    && chown --recursive $APP_UID:$APP_UID /var/lib/codex /tmp/car-expense-codex-work

WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080 \
    CODEX_HOME=/var/lib/codex \
    CODEX_WORK_ROOT=/tmp/car-expense-codex-work
EXPOSE 8080
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "CarExpenseCalculator.CodexExtractor.dll"]
