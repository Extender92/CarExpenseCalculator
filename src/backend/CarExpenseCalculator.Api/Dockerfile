FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/backend/CarExpenseCalculator.Api/CarExpenseCalculator.Api.csproj src/backend/CarExpenseCalculator.Api/
COPY src/backend/CarExpenseCalculator.Core/CarExpenseCalculator.Core.csproj src/backend/CarExpenseCalculator.Core/
COPY src/backend/CarExpenseCalculator.Infrastructure/CarExpenseCalculator.Infrastructure.csproj src/backend/CarExpenseCalculator.Infrastructure/
RUN dotnet restore src/backend/CarExpenseCalculator.Api/CarExpenseCalculator.Api.csproj

COPY src/backend src/backend
RUN dotnet publish src/backend/CarExpenseCalculator.Api/CarExpenseCalculator.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "CarExpenseCalculator.Api.dll"]
