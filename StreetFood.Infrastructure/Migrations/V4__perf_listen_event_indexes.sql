-- Improve write/read performance for listen-event de-duplication under load.
CREATE INDEX IF NOT EXISTS idx_poi_audio_listen_poi_device_created
ON poi_audio_listen_events (poi_id, device_id, created_at DESC);
