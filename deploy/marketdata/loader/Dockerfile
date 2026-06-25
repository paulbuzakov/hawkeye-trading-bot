# HTB.MarketData.Loader — the market-data ingestion console app.
#
# Build context: repository root (needs the solution + csproj graph to restore).
#   docker build -f deploy/loader/Dockerfile -t htb/marketdata-loader .

# ---- build / publish -------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore against the csproj graph first so this layer is cached unless a
# project file (i.e. a dependency) actually changes.
COPY nuget.config HTB.slnx ./
COPY src/marketdata/HTB.MarketData.Loader/HTB.MarketData.Loader.csproj src/marketdata/HTB.MarketData.Loader/
COPY src/shared/HTB.Shared/HTB.Shared.csproj src/shared/HTB.Shared/
RUN dotnet restore src/marketdata/HTB.MarketData.Loader/HTB.MarketData.Loader.csproj

# Copy the rest of the sources and publish a framework-dependent build.
COPY src/ src/
RUN dotnet publish src/marketdata/HTB.MarketData.Loader/HTB.MarketData.Loader.csproj \
        -c Release --no-restore -o /app

# ---- runtime ---------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app

# Npgsql loads libgssapi_krb5 for Kerberos/GSSAPI auth; the slim runtime image
# omits it, so connecting to Postgres would crash without this package.
RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app ./

# Run as the non-root user baked into the .NET runtime images.
USER $APP_UID

ENTRYPOINT ["dotnet", "HTB.MarketData.Loader.dll"]
