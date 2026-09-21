FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:e1ffd2a92ae84c1291bc1b6887501f8af98e6331e7af6d4c8d37168c5e87a64c AS build
WORKDIR /source
RUN dotnet --version | grep --fixed-strings "10.0.400"

COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/backend/CarExpenseCalculator.Api/CarExpenseCalculator.Api.csproj src/backend/CarExpenseCalculator.Api/
COPY src/backend/CarExpenseCalculator.Core/CarExpenseCalculator.Core.csproj src/backend/CarExpenseCalculator.Core/
COPY src/backend/CarExpenseCalculator.Infrastructure/CarExpenseCalculator.Infrastructure.csproj src/backend/CarExpenseCalculator.Infrastructure/
COPY src/backend/CarExpenseCalculator.Extraction.Contracts/CarExpenseCalculator.Extraction.Contracts.csproj src/backend/CarExpenseCalculator.Extraction.Contracts/
RUN dotnet restore src/backend/CarExpenseCalculator.Api/CarExpenseCalculator.Api.csproj

COPY src/backend src/backend
RUN dotnet publish src/backend/CarExpenseCalculator.Api/CarExpenseCalculator.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0@sha256:a4556ed033fa96f984bb7a8d348851cb2d36b1281dd2420070045f664fbb5f94 AS runtime
WORKDIR /app
RUN apt-get update \
    && apt-get install --yes --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "CarExpenseCalculator.Api.dll"]
