-- Ensure the TimescaleDB extension exists in the target database on first boot.
-- The EF migrations also run this idempotently (CREATE EXTENSION IF NOT EXISTS),
-- so this is a belt-and-suspenders step that leaves the DB ready even before the
-- migrations container has run.
CREATE EXTENSION IF NOT EXISTS timescaledb;
