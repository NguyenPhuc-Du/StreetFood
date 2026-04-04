-- Kích hoạt theo thiết bị (install_id), không cần đăng nhập
CREATE TABLE IF NOT EXISTS device_activations (
    install_id VARCHAR(64) PRIMARY KEY,
    expires_at TIMESTAMPTZ NOT NULL,
    plan_label VARCHAR(50),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_device_activations_expires ON device_activations (expires_at);
