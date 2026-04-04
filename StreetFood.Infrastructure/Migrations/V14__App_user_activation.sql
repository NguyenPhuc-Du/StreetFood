-- Kích hoạt tour trên app di động (role app), lưu hạn trên server
ALTER TABLE users ADD COLUMN IF NOT EXISTS app_activation_expires_at TIMESTAMPTZ NULL;
