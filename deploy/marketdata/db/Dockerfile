# TimescaleDB (PostgreSQL 17) — the candle store for HTB market data.
#
# Build context: deploy/database
# The relational schema and the `candles` hypertable are created by the
# migrations container; this image only ships the engine plus a first-boot
# init script that ensures the TimescaleDB extension is present.
FROM timescale/timescaledb:latest-pg17

# Scripts in /docker-entrypoint-initdb.d run once, on an empty data directory.
COPY initdb/ /docker-entrypoint-initdb.d/
