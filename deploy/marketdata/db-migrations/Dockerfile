# HTB.MarketData.Migrations — applies EF Core migrations to the candle store.
#
# HTB.MarketData.Migrations is a class library, so we package the migrations as a
# self-applying EF "migration bundle": a small executable that runs every pending
# migration and exits. It resolves its connection string through the design-time
# MarketDataDbContextFactory, which reads the HTB_MARKETDATA_DB env var.
#
# Build context: repository root.
#   docker build -f deploy/migrations/Dockerfile -t htb/marketdata-migrations .

# ---- build the migration bundle -------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
ENV PATH="$PATH:/root/.dotnet/tools"

# EF Core CLI, used to produce the bundle.
RUN dotnet tool install --global dotnet-ef --version 10.0.4

# Restore against the csproj graph first for layer caching.
COPY nuget.config HTB.slnx ./
COPY src/marketdata/HTB.MarketData.Migrations/HTB.MarketData.Migrations.csproj src/marketdata/HTB.MarketData.Migrations/
COPY src/shared/HTB.Shared/HTB.Shared.csproj src/shared/HTB.Shared/
RUN dotnet restore src/marketdata/HTB.MarketData.Migrations/HTB.MarketData.Migrations.csproj

COPY src/ src/
RUN dotnet ef migrations bundle \
        --project src/marketdata/HTB.MarketData.Migrations/HTB.MarketData.Migrations.csproj \
        --startup-project src/marketdata/HTB.MarketData.Migrations/HTB.MarketData.Migrations.csproj \
        --configuration Release \
        --output /app/efbundle

# ---- runtime ---------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app

# Npgsql loads libgssapi_krb5 for Kerberos/GSSAPI auth; the slim runtime image
# omits it, so the bundle would crash at startup without this package.
RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/efbundle ./efbundle
RUN chmod +x ./efbundle

USER $APP_UID

# The bundle reads the connection string from HTB_MARKETDATA_DB (via the
# design-time factory). Override at runtime with `--connection "<conn>"` if needed.
ENTRYPOINT ["./efbundle"]
