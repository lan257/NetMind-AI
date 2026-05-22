-- NetMind P3.2 add canvas position for mind map nodes.
-- Target database: PostgreSQL
-- This script is idempotent and safe to run repeatedly.

BEGIN;

ALTER TABLE node
    ADD COLUMN IF NOT EXISTS position_x DOUBLE PRECISION;

ALTER TABLE node
    ADD COLUMN IF NOT EXISTS position_y DOUBLE PRECISION;

COMMIT;
