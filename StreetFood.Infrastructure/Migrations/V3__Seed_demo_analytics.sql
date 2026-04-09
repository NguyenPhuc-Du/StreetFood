-- Dữ liệu ảo: heatmap, tuyến đi, lượt ghé, thời lượng nghe — phục vụ dashboard admin.
-- Chạy sau V2 (đã có POI 1..4).

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
('tour-b', 4, NOW() - INTERVAL '2 days', NOW() - INTERVAL '2 days' + INTERVAL '18 minutes', 18),
('sf-demo-3', 1, NOW() - INTERVAL '30 hours', NOW() - INTERVAL '30 hours' + INTERVAL '16 minutes', 16),
('sf-demo-3', 2, NOW() - INTERVAL '28 hours', NOW() - INTERVAL '28 hours' + INTERVAL '22 minutes', 22),
('sf-demo-4', 3, NOW() - INTERVAL '26 hours', NOW() - INTERVAL '26 hours' + INTERVAL '14 minutes', 14),
('sf-demo-4', 4, NOW() - INTERVAL '24 hours', NOW() - INTERVAL '24 hours' + INTERVAL '11 minutes', 11),
('tour-c', 2, NOW() - INTERVAL '20 hours', NOW() - INTERVAL '20 hours' + INTERVAL '27 minutes', 27),
('tour-c', 4, NOW() - INTERVAL '16 hours', NOW() - INTERVAL '16 hours' + INTERVAL '19 minutes', 19);

INSERT INTO location_logs (deviceid, latitude, longitude, createdat) VALUES
('sf-demo-3', 10.75495, 106.66672, NOW() - INTERVAL '20 hours'),
('sf-demo-3', 10.75520, 106.66695, NOW() - INTERVAL '19 hours'),
('sf-demo-4', 10.75605, 106.66810, NOW() - INTERVAL '18 hours'),
('sf-demo-4', 10.75630, 106.66855, NOW() - INTERVAL '17 hours'),
('tour-c', 10.75955, 106.68012, NOW() - INTERVAL '15 hours'),
('tour-c', 10.75972, 106.68036, NOW() - INTERVAL '13 hours');

INSERT INTO movement_paths (deviceid, frompoiid, topoiid, createdat) VALUES
('sf-demo-3', 1, 2, NOW() - INTERVAL '29 hours'),
('sf-demo-3', 2, 4, NOW() - INTERVAL '27 hours'),
('sf-demo-4', 4, 3, NOW() - INTERVAL '23 hours'),
('sf-demo-4', 3, 1, NOW() - INTERVAL '21 hours'),
('tour-c', 2, 4, NOW() - INTERVAL '15 hours'),
('tour-c', 4, 1, NOW() - INTERVAL '11 hours');

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
(4, 99, 'sf-demo-1', NOW() - INTERVAL '1 hour'),
(1, 115, 'sf-demo-3', NOW() - INTERVAL '50 minutes'),
(2, 165, 'sf-demo-4', NOW() - INTERVAL '42 minutes'),
(3, 80, 'tour-c', NOW() - INTERVAL '34 minutes'),
(4, 145, 'tour-c', NOW() - INTERVAL '26 minutes'),
(2, 132, 'sf-demo-3', NOW() - INTERVAL '18 minutes');
