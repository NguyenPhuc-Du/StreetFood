-- V12: Soft-delete support for Foods
-- Safe to run multiple times (DbInitializer executes all .sql files on startup).

ALTER TABLE Foods
    ADD COLUMN IF NOT EXISTS IsHidden BOOLEAN NOT NULL DEFAULT FALSE;

-- Optimize common query pattern:
--   WHERE PoiId = @PoiId AND COALESCE(IsHidden, FALSE) = FALSE
CREATE INDEX IF NOT EXISTS idx_foods_poi_visible
    ON Foods (PoiId)
    WHERE COALESCE(IsHidden, FALSE) = FALSE;

