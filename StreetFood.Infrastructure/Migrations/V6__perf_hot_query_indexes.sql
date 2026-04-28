-- Additional hot-path indexes for faster API reads under load.
-- Keep idempotent for existing databases.

-- POI list/detail/top: filter translations by language then join by POI.
CREATE INDEX IF NOT EXISTS idx_poi_translations_lang_poi
ON poi_translations (languagecode, poiid);

-- Top POI query: scan recent visits then aggregate by POI.
CREATE INDEX IF NOT EXISTS idx_device_visits_enter_poi
ON device_visits (entertime DESC, poiid);

-- Heatmap/online analytics windows: recent logs by time and device.
CREATE INDEX IF NOT EXISTS idx_location_logs_created_device
ON location_logs (createdat DESC, deviceid);

-- Premium join/filter: active subscriptions not expired.
CREATE INDEX IF NOT EXISTS idx_poi_premium_subscriptions_active_end
ON poi_premium_subscriptions (end_at DESC, poi_id)
WHERE status = 'active';

-- POI owner joins.
CREATE INDEX IF NOT EXISTS idx_restaurant_owners_poi_user
ON restaurant_owners (poiid, userid);

-- Common admin/vendor user filters.
CREATE INDEX IF NOT EXISTS idx_users_role_hidden
ON users (role, ishidden, id);
