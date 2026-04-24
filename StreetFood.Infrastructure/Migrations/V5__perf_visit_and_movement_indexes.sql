-- Improve concurrency performance for visit/session/movement queries.
CREATE INDEX IF NOT EXISTS idx_device_visits_device_poi_open_enter_desc
ON device_visits (deviceid, poiid, exittime, entertime DESC);

CREATE INDEX IF NOT EXISTS idx_device_visits_device_poi_exit_desc
ON device_visits (deviceid, poiid, exittime DESC);

CREATE INDEX IF NOT EXISTS idx_movement_paths_device_created_desc
ON movement_paths (deviceid, createdat DESC);
