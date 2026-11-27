FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /source

COPY global.json .
COPY Directory.Build.props .
COPY Directory.Packages.props .

COPY src/InventoryManagement.Domain/InventoryManagement.Domain.csproj src/InventoryManagement.Domain/
COPY src/InventoryManagement.Application/InventoryManagement.Application.csproj src/InventoryManagement.Application/
COPY src/InventoryManagement.Infrastructure/InventoryManagement.Infrastructure.csproj src/InventoryManagement.Infrastructure/
COPY src/InventoryManagement.Api/InventoryManagement.Api.csproj src/InventoryManagement.Api/

RUN dotnet restore src/InventoryManagement.Api/InventoryManagement.Api.csproj

COPY src/ src/

RUN dotnet publish src/InventoryManagement.Api/InventoryManagement.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

USER root

RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080

EXPOSE 8080

COPY --from=build /app/publish .

USER $APP_UID

ENTRYPOINT ["dotnet", "InventoryManagement.Api.dll"]
