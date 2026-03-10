CREATE INDEX idx_poi_location ON POIs (Latitude, Longitude);
CREATE INDEX idx_device_visits_poi ON Device_Visits (PoiId);
CREATE INDEX idx_location_logs_device ON Location_Logs (DeviceId);
CREATE INDEX idx_movement_paths ON Movement_Paths (FromPoiId, ToPoiId);
CREATE INDEX idx_foods_poi ON Foods (PoiId);
CREATE INDEX idx_audio_poi ON Restaurant_Audio (PoiId);