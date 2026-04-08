-- StreetFood: schema gộp (thay thế V1–V16 cũ). Chạy trên PostgreSQL 14+.
-- Bảng và cột dùng chữ thường (PostgreSQL chuẩn hóa identifier không có ngoặc kép).

CREATE TABLE languages (
    code VARCHAR(5) PRIMARY KEY,
    name VARCHAR(50) NOT NULL
);

CREATE TABLE users (
    id SERIAL PRIMARY KEY,
    username VARCHAR(50) UNIQUE NOT NULL,
    password TEXT NOT NULL,
    role VARCHAR(20) NOT NULL,
    createdat TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    ishidden BOOLEAN DEFAULT FALSE,
    email VARCHAR(255),
    app_activation_expires_at TIMESTAMPTZ NULL
);

CREATE TABLE pois (
    id SERIAL PRIMARY KEY,
    latitude DOUBLE PRECISION NOT NULL,
    longitude DOUBLE PRECISION NOT NULL,
    radius INT DEFAULT 80,
    address TEXT,
    imageurl TEXT,
    createdat TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    scriptsubmissionstate VARCHAR(40) DEFAULT 'awaiting_vendor'
);

CREATE TABLE poi_translations (
    id SERIAL PRIMARY KEY,
    poiid INT NOT NULL,
    languagecode VARCHAR(5) NOT NULL,
    name VARCHAR(255) NOT NULL,
    description TEXT,
    CONSTRAINT fk_poi_translations_poi FOREIGN KEY (poiid) REFERENCES pois(id) ON DELETE CASCADE,
    CONSTRAINT fk_poi_translations_lang FOREIGN KEY (languagecode) REFERENCES languages(code)
);

CREATE UNIQUE INDEX idx_poi_language ON poi_translations(poiid, languagecode);

CREATE TABLE restaurant_details (
    id SERIAL PRIMARY KEY,
    poiid INT UNIQUE,
    openinghours VARCHAR(100),
    phone VARCHAR(20),
    CONSTRAINT fk_restaurant_details_poi FOREIGN KEY (poiid) REFERENCES pois(id) ON DELETE CASCADE
);

CREATE TABLE restaurant_owners (
    id SERIAL PRIMARY KEY,
    userid INT,
    poiid INT,
    CONSTRAINT fk_restaurant_owners_user FOREIGN KEY (userid) REFERENCES users(id),
    CONSTRAINT fk_restaurant_owners_poi FOREIGN KEY (poiid) REFERENCES pois(id)
);

CREATE TABLE foods (
    id SERIAL PRIMARY KEY,
    poiid INT NOT NULL,
    name VARCHAR(255) NOT NULL,
    description TEXT,
    price INT,
    imageurl TEXT,
    ishidden BOOLEAN NOT NULL DEFAULT FALSE,
    CONSTRAINT fk_foods_poi FOREIGN KEY (poiid) REFERENCES pois(id) ON DELETE CASCADE
);

CREATE INDEX idx_foods_poi_visible ON foods (poiid) WHERE COALESCE(ishidden, FALSE) = FALSE;

CREATE TABLE restaurant_audio (
    id SERIAL PRIMARY KEY,
    poiid INT NOT NULL,
    languagecode VARCHAR(5) NOT NULL,
    audiourl TEXT NOT NULL,
    CONSTRAINT fk_restaurant_audio_poi FOREIGN KEY (poiid) REFERENCES pois(id) ON DELETE CASCADE,
    CONSTRAINT fk_restaurant_audio_lang FOREIGN KEY (languagecode) REFERENCES languages(code)
);

CREATE UNIQUE INDEX idx_audio_language ON restaurant_audio(poiid, languagecode);

CREATE TABLE script_change_requests (
    id SERIAL PRIMARY KEY,
    poiid INT,
    languagecode VARCHAR(5),
    newscript TEXT,
    status VARCHAR(20) DEFAULT 'pending',
    createdby INT,
    createdat TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT fk_script_requests_poi FOREIGN KEY (poiid) REFERENCES pois(id),
    CONSTRAINT fk_script_requests_user FOREIGN KEY (createdby) REFERENCES users(id)
);

CREATE TABLE device_visits (
    id SERIAL PRIMARY KEY,
    deviceid VARCHAR(100),
    poiid INT,
    entertime TIMESTAMP,
    exittime TIMESTAMP,
    duration INT,
    CONSTRAINT fk_device_visits_poi FOREIGN KEY (poiid) REFERENCES pois(id)
);

CREATE TABLE location_logs (
    id SERIAL PRIMARY KEY,
    deviceid VARCHAR(100),
    latitude DOUBLE PRECISION,
    longitude DOUBLE PRECISION,
    createdat TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_location_logs_created ON location_logs (createdat);

CREATE TABLE movement_paths (
    id SERIAL PRIMARY KEY,
    deviceid VARCHAR(100),
    frompoiid INT,
    topoiid INT,
    createdat TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_movement_paths_created ON movement_paths (createdat);

CREATE TABLE device_activations (
    install_id VARCHAR(64) PRIMARY KEY,
    expires_at TIMESTAMPTZ NOT NULL,
    plan_label VARCHAR(50),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_device_activations_expires ON device_activations (expires_at);

CREATE TABLE poi_audio_listen_events (
    id BIGSERIAL PRIMARY KEY,
    poi_id INT NOT NULL REFERENCES pois(id) ON DELETE CASCADE,
    duration_seconds INT NOT NULL CHECK (duration_seconds >= 3 AND duration_seconds <= 7200),
    device_id VARCHAR(64),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_poi_audio_listen_poi ON poi_audio_listen_events (poi_id);
CREATE INDEX idx_poi_audio_listen_created ON poi_audio_listen_events (created_at DESC);
