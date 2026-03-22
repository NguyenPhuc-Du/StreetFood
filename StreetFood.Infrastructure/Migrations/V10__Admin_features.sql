ALTER TABLE Users ADD COLUMN IF NOT EXISTS IsHidden BOOLEAN DEFAULT FALSE;
ALTER TABLE Users ADD COLUMN IF NOT EXISTS Email VARCHAR(255);

ALTER TABLE POIs ADD COLUMN IF NOT EXISTS ScriptSubmissionState VARCHAR(40) DEFAULT 'awaiting_vendor';

CREATE INDEX IF NOT EXISTS idx_location_logs_created ON Location_Logs (CreatedAt);
CREATE INDEX IF NOT EXISTS idx_movement_paths_created ON Movement_Paths (CreatedAt);
