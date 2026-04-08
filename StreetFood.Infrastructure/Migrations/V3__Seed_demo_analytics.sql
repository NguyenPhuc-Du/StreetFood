-- Dữ liệu ảo: heatmap, tuyến đi, lượt ghé, thời lượng nghe — phục vụ dashboard admin.
-- Chạy sau V2 (đã có POI 1..4).

INSERT INTO device_activations (install_id, expires_at, plan_label) VALUES
('demo-install-001', NOW() + INTERVAL '365 days', 'demo'),
('demo-install-002', NOW() + INTERVAL '180 days', 'trial');

INSERT INTO location_logs (deviceid, latitude, longitude, createdat) VALUES
('sf-demo-1', 10.75490, 106.66660, NOW() - INTERVAL '14 days'),
('sf-demo-1', 10.75510, 106.66690, NOW() - INTERVAL '12 days'),
('sf-demo-1', 10.75530, 106.66710, NOW() - INTERVAL '10 days'),
('sf-demo-2', 10.75600, 106.66800, NOW() - INTERVAL '9 days'),
('sf-demo-2', 10.75620, 106.66840, NOW() - INTERVAL '7 days'),
('tour-a', 10.75950, 106.68000, NOW() - INTERVAL '6 days'),
('tour-a', 10.75970, 106.68030, NOW() - INTERVAL '5 days'),
('tour-b', 10.75450, 106.66600, NOW() - INTERVAL '4 days'),
('tour-b', 10.75470, 106.66630, NOW() - INTERVAL '3 days'),
('tour-b', 10.75490, 106.66660, NOW() - INTERVAL '2 days'),
('sf-demo-1', 10.75500, 106.66670, NOW() - INTERVAL '1 day'),
('sf-demo-2', 10.75610, 106.66820, NOW() - INTERVAL '18 hours'),
('tour-a', 10.75960, 106.68010, NOW() - INTERVAL '10 hours'),
('tour-b', 10.75480, 106.66650, NOW() - INTERVAL '3 hours');

INSERT INTO movement_paths (deviceid, frompoiid, topoiid, createdat) VALUES
('sf-demo-1', 1, 2, NOW() - INTERVAL '8 days'),
('sf-demo-1', 2, 3, NOW() - INTERVAL '6 days'),
('sf-demo-2', 1, 4, NOW() - INTERVAL '5 days'),
('tour-a', 3, 4, NOW() - INTERVAL '4 days'),
('tour-b', 2, 1, NOW() - INTERVAL '3 days'),
('sf-demo-1', 4, 1, NOW() - INTERVAL '2 days'),
('tour-a', 1, 3, NOW() - INTERVAL '1 day'),
('sf-demo-2', 3, 2, NOW() - INTERVAL '12 hours');

INSERT INTO device_visits (deviceid, poiid, entertime, exittime, duration) VALUES
('sf-demo-1', 1, NOW() - INTERVAL '7 days', NOW() - INTERVAL '7 days' + INTERVAL '12 minutes', 12),
('sf-demo-2', 2, NOW() - INTERVAL '5 days', NOW() - INTERVAL '5 days' + INTERVAL '25 minutes', 25),
('tour-a', 3, NOW() - INTERVAL '4 days', NOW() - INTERVAL '4 days' + INTERVAL '8 minutes', 8),
('tour-b', 4, NOW() - INTERVAL '2 days', NOW() - INTERVAL '2 days' + INTERVAL '18 minutes', 18);

INSERT INTO poi_audio_listen_events (poi_id, duration_seconds, device_id, created_at) VALUES
(1, 45, 'sf-demo-1', NOW() - INTERVAL '10 days'),
(1, 120, 'sf-demo-2', NOW() - INTERVAL '9 days'),
(1, 90, 'tour-a', NOW() - INTERVAL '8 days'),
(2, 200, 'sf-demo-1', NOW() - INTERVAL '7 days'),
(2, 150, 'tour-b', NOW() - INTERVAL '6 days'),
(2, 95, 'sf-demo-2', NOW() - INTERVAL '5 days'),
(3, 60, 'tour-a', NOW() - INTERVAL '4 days'),
(3, 88, 'sf-demo-1', NOW() - INTERVAL '3 days'),
(1, 72, 'tour-b', NOW() - INTERVAL '3 days'),
(4, 110, 'sf-demo-2', NOW() - INTERVAL '2 days'),
(4, 95, 'tour-a', NOW() - INTERVAL '2 days'),
(4, 130, 'sf-demo-1', NOW() - INTERVAL '1 day'),
(3, 55, 'sf-demo-2', NOW() - INTERVAL '1 day'),
(2, 180, 'tour-a', NOW() - INTERVAL '20 hours'),
(1, 40, 'tour-b', NOW() - INTERVAL '15 hours'),
(4, 105, 'tour-b', NOW() - INTERVAL '8 hours'),
(1, 65, 'sf-demo-1', NOW() - INTERVAL '5 hours'),
(2, 140, 'sf-demo-2', NOW() - INTERVAL '4 hours'),
(3, 70, 'tour-a', NOW() - INTERVAL '2 hours'),
(4, 99, 'sf-demo-1', NOW() - INTERVAL '1 hour');
