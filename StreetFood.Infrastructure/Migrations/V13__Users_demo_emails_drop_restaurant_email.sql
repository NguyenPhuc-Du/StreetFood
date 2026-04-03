-- Email liên hệ chỉ dùng trên bảng Users (V10 đã có cột Email).
-- Gỡ cột email khỏi Restaurant_Details nếu đã từng thêm (tránh trùng với user).
ALTER TABLE Restaurant_Details DROP COLUMN IF EXISTS email;

-- Điền email ảo cho các tài khoản chưa có email (định dạng hợp lệ).
UPDATE users
SET email = lower(username) || '@streetfood.demo'
WHERE email IS NULL OR trim(coalesce(email, '')) = '';
