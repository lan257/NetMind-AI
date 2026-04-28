-- NetMind P0.3 logical delete migration.
-- Target database: PostgreSQL
-- This script is idempotent and safe to run repeatedly after P0.2 Init.sql.

BEGIN;

ALTER TABLE mind_map
    ADD COLUMN IF NOT EXISTS is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
    ADD COLUMN IF NOT EXISTS deleted_at TIMESTAMPTZ NULL;

ALTER TABLE node
    ADD COLUMN IF NOT EXISTS is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
    ADD COLUMN IF NOT EXISTS deleted_at TIMESTAMPTZ NULL;

ALTER TABLE node_relation
    ADD COLUMN IF NOT EXISTS is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
    ADD COLUMN IF NOT EXISTS deleted_at TIMESTAMPTZ NULL;

CREATE INDEX IF NOT EXISTS idx_mind_map_not_deleted
    ON mind_map (id)
    WHERE is_deleted = FALSE;

CREATE INDEX IF NOT EXISTS idx_node_map_id_not_deleted
    ON node (map_id)
    WHERE is_deleted = FALSE;

CREATE INDEX IF NOT EXISTS idx_node_parent_id_not_deleted
    ON node (parent_id)
    WHERE is_deleted = FALSE;

CREATE INDEX IF NOT EXISTS idx_node_map_parent_order_not_deleted
    ON node (map_id, parent_id, order_no)
    WHERE is_deleted = FALSE;

CREATE INDEX IF NOT EXISTS idx_node_relation_source_id_not_deleted
    ON node_relation (source_id)
    WHERE is_deleted = FALSE;

CREATE INDEX IF NOT EXISTS idx_node_relation_target_id_not_deleted
    ON node_relation (target_id)
    WHERE is_deleted = FALSE;

CREATE INDEX IF NOT EXISTS idx_node_relation_map_id_not_deleted
    ON node_relation (map_id)
    WHERE is_deleted = FALSE;

COMMIT;
