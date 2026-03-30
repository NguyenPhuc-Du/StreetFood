-- Dữ liệu mẫu (schema V1): chỉ chèn khi chưa có dòng nào — phục vụ demo heatmap / tuyến trên bản đồ admin.
-- Tọa độ gần khu vực POI mẫu (Quận 5, TP.HCM).

INSERT INTO location_logs (deviceid, latitude, longitude, createdat)
SELECT * FROM (
  SELECT 'sf-demo'::text, 10.7550::double precision, 106.6665::double precision, NOW() - INTERVAL '10 days'
  UNION ALL SELECT 'sf-demo', 10.7552, 106.6668, NOW() - INTERVAL '5 days'
  UNION ALL SELECT 'sf-demo', 10.7548, 106.6672, NOW() - INTERVAL '1 day'
) q
WHERE NOT EXISTS (SELECT 1 FROM location_logs LIMIT 1);

INSERT INTO movement_paths (deviceid, frompoiid, topoiid, createdat)
SELECT 'sf-demo', 1, 2, NOW() - INTERVAL '3 days'
WHERE EXISTS (SELECT 1 FROM pois WHERE id = 1)
  AND EXISTS (SELECT 1 FROM pois WHERE id = 2)
  AND NOT EXISTS (SELECT 1 FROM movement_paths LIMIT 1);

INSERT INTO movement_paths (deviceid, frompoiid, topoiid, createdat)
SELECT 'sf-demo', 2, 3, NOW() - INTERVAL '1 day'
WHERE EXISTS (SELECT 1 FROM pois WHERE id = 2)
  AND EXISTS (SELECT 1 FROM pois WHERE id = 3)
  AND (SELECT COUNT(*)::int FROM movement_paths) <= 1;
