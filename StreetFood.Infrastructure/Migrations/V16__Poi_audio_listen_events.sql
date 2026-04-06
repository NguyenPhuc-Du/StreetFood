-- Sự kiện nghe audio theo POI (app gửi tổng giây mỗi lần dừng / đổi bài) — phục vụ TB thời lượng nghe trên admin.
CREATE TABLE IF NOT EXISTS poi_audio_listen_events (
    id BIGSERIAL PRIMARY KEY,
    poi_id INT NOT NULL REFERENCES pois(id) ON DELETE CASCADE,
    duration_seconds INT NOT NULL CHECK (duration_seconds >= 1 AND duration_seconds <= 7200),
    device_id VARCHAR(64),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_poi_audio_listen_poi ON poi_audio_listen_events (poi_id);
CREATE INDEX IF NOT EXISTS idx_poi_audio_listen_created ON poi_audio_listen_events (created_at DESC);
