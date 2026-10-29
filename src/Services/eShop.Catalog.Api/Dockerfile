FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/Services/eShop.Catalog.Api/eShop.Catalog.Api.csproj", "src/Services/eShop.Catalog.Api/"]
COPY ["src/BuildingBlocks/BuildingBlock/BuildingBlock.csproj", "src/BuildingBlocks/BuildingBlock/"]
COPY ["src/BuildingBlocks/BuildBlock.Logging/BuildBlock.Logging.csproj", "src/BuildingBlocks/BuildBlock.Logging/"]
RUN dotnet restore "src/Services/eShop.Catalog.Api/eShop.Catalog.Api.csproj"
COPY src/ src/
WORKDIR /src/src/Services/eShop.Catalog.Api
RUN dotnet publish "eShop.Catalog.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false --no-restore

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "eShop.Catalog.Api.dll"]
