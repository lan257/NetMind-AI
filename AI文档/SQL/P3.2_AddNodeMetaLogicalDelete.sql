-- NetMind P3.2 database migration script.
-- Add logical delete columns to node_meta table.
-- This script is idempotent and safe to run repeatedly.

BEGIN;

-- Add is_deleted column if not exists
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_name = 'node_meta'
          AND column_name = 'is_deleted'
    ) THEN
        ALTER TABLE node_meta ADD COLUMN is_deleted BOOLEAN NOT NULL DEFAULT FALSE;
    END IF;
END;
$$;

-- Add deleted_at column if not exists
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_name = 'node_meta'
          AND column_name = 'deleted_at'
    ) THEN
        ALTER TABLE node_meta ADD COLUMN deleted_at TIMESTAMPTZ NULL;
    END IF;
END;
$$;

-- Add index for logical delete performance
CREATE INDEX IF NOT EXISTS idx_node_meta_is_deleted
    ON node_meta (is_deleted)
    WHERE is_deleted = FALSE;

COMMIT;
