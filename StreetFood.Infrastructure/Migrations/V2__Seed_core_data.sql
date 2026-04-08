-- Dữ liệu nền: ngôn ngữ, tài khoản, POI, món, audio, liên kết chủ quán.
-- Mật khẩu demo: admin123 (admin), vendor123 (vendor). Đổi trên môi trường thật.

INSERT INTO languages (code, name) VALUES
('vi', 'Vietnamese'),
('en', 'English'),
('cn', 'Chinese'),
('ja', 'Japanese'),
('ko', 'Korean');

INSERT INTO users (username, password, role, email) VALUES
('admin', 'admin123', 'admin', 'admin@streetfood.demo'),
('highlands_owner', 'vendor123', 'vendor', 'highlands.owner@streetfood.demo'),
('lauchi_owner', 'vendor123', 'vendor', 'lauchi.owner@streetfood.demo'),
('mixue_owner', 'vendor123', 'vendor', 'mixue.owner@streetfood.demo'),
('pho_owner', 'vendor123', 'vendor', 'pho.owner@streetfood.demo');

INSERT INTO pois (latitude, longitude, radius, address, imageurl, scriptsubmissionstate) VALUES
(10.75490, 106.66660, 85, '98 Nguyen Tri Phuong, Ward 7, District 5, Ho Chi Minh City',
 'https://pub-a1bf3067219c40438a869f85033c0815.r2.dev/restaurants/1/images/main.png', 'approved'),
(10.75515, 106.66685, 90, '110 Nguyen Tri Phuong, Ward 7, District 5, Ho Chi Minh City',
 'https://pub-a1bf3067219c40438a869f85033c0815.r2.dev/restaurants/2/images/main.png', 'approved'),
(10.759641, 106.680247, 75, '124 Nguyen Tri Phuong, Ward 7, District 5, Ho Chi Minh City',
 'https://pub-a1bf3067219c40438a869f85033c0815.r2.dev/restaurants/3/images/main.png', 'approved'),
(10.75620, 106.66850, 80, '242 Tran Hung Dao, Ward 11, District 5, Ho Chi Minh City',
 'https://pub-a1bf3067219c40438a869f85033c0815.r2.dev/restaurants/4/images/main.png', 'approved');

INSERT INTO poi_translations (poiid, languagecode, name, description) VALUES
(1, 'vi', 'Highlands Coffee', 'Chào mừng đến Highlands Coffee — không gian cà phê phố Việt, đồ uống đa dạng và chỗ ngồi thoải mái.'),
(1, 'en', 'Highlands Coffee', 'Welcome to Highlands Coffee — Vietnamese-style coffee house with drinks and a cozy place to relax.'),
(1, 'cn', 'Highlands Coffee', '欢迎来到高地咖啡，享受香浓咖啡与舒适空间。'),
(1, 'ja', 'Highlands Coffee', 'ハイランズコーヒーへようこそ。ベトナムの味わいと落ち着いた空間をお楽しみください。'),
(1, 'ko', 'Highlands Coffee', '하이랜드 커피에 오신 것을 환영합니다. 편안한 공간에서 음료를 즐겨 보세요.'),

(2, 'vi', 'Có Chí Thì Nên', 'Quán lẩu đường phố — nước dù đậm đà, đồ nhúng tươi, phục vụ cả khách đi một mình lẫn nhóm bạn.'),
(2, 'en', 'Co Chi Thi Nen', 'Street hotpot spot — rich broth, fresh ingredients, great for groups or a quick meal.'),
(2, 'cn', 'Có Chí Thì Nên', '街头火锅，汤底浓郁，食材新鲜，适合朋友小聚。'),
(2, 'ja', 'Có Chí Thì Nên', 'ストリート鍋の名店。濃厚スープと新鮮具材をどうぞ。'),
(2, 'ko', 'Có Chí Thì Nên', '길거리 훠궈 맛집. 진한 육수와 신선한 재료를 만나보세요.'),

(3, 'vi', 'Mixue', 'Trà sữa & kem — giá mềm, vị thanh mát, lý tưởng mang đi dưới trời nóng Sài Gòn.'),
(3, 'en', 'Mixue', 'Milk tea and ice cream — affordable, refreshing, perfect for takeaway in warm weather.'),
(3, 'cn', 'Mixue', '奶茶与冰淇淋，价格实惠，清爽解暑。'),
(3, 'ja', 'Mixue', 'タピオカミルクティーとアイス。手頃な価格でさっぱり味わい。'),
(3, 'ko', 'Mixue', '밀크티와 아이스크림. 가성비 좋은 시원한 한 잔.'),

(4, 'vi', 'Phở Bò Gia Truyền', 'Phở bò ninh xương nhiều giờ, bánh phở mềm, thịt tái/chín tùy chọn — quán nhỏ nhưng đông khách quanh năm.'),
(4, 'en', 'Pho Bo Gia Truyen', 'Slow-simmered beef pho, soft noodles, rare or well-done beef — a busy neighborhood favorite.'),
(4, 'cn', 'Phở Bò Gia Truyền', '传统牛肉河粉，汤底醇厚，米粉爽滑，深受街坊喜爱。'),
(4, 'ja', 'Phở Bò Gia Truyền', '牛骨をじっくり煮込んだフォー。地元の人気店です。'),
(4, 'ko', 'Phở Bò Gia Truyền', '오랜 시간 우려낸 쇠고기 쌀국수. 동네 단골 메뉴.');

INSERT INTO restaurant_details (poiid, openinghours, phone) VALUES
(1, '07:00 - 22:00', '0283999888'),
(2, '09:00 - 23:00', '0283777666'),
(3, '10:00 - 23:30', '0283555444'),
(4, '06:30 - 21:00', '0283888999');

INSERT INTO restaurant_owners (userid, poiid) VALUES
(2, 1),
(3, 2),
(4, 3),
(5, 4);

INSERT INTO foods (poiid, name, description, price, imageurl, ishidden) VALUES
(1, 'Den Da', 'Vietnamese black iced coffee', 35000, 'https://pub-a1bf3067219c40438a869f85033c0815.r2.dev/restaurants/1/dishes/1.jpg', FALSE),
(1, 'Nau Da', 'Vietnamese milk iced coffee', 39000, 'https://pub-a1bf3067219c40438a869f85033c0815.r2.dev/restaurants/1/dishes/2.jpg', FALSE),
(1, 'Phindi Hanh Nhan', 'Coffee with almond flavor', 45000, 'https://pub-a1bf3067219c40438a869f85033c0815.r2.dev/restaurants/1/dishes/3.jpg', FALSE),
(1, 'Bac Xiu Da', 'Milk coffee with extra milk', 42000, 'https://pub-a1bf3067219c40438a869f85033c0815.r2.dev/restaurants/1/dishes/4.jpg', FALSE),
(1, 'Banh Mi Que', 'Mini Vietnamese baguette', 25000, 'https://pub-a1bf3067219c40438a869f85033c0815.r2.dev/restaurants/1/dishes/5.jpg', FALSE),

(2, 'Lau Thai Bo', 'Thai hotpot with beef', 199000, 'https://pub-a1bf3067219c40438a869f85033c0815.r2.dev/restaurants/2/dishes/1.jpg', FALSE),
(2, 'Lau Malatang Bo', 'Malatang beef hotpot', 209000, 'https://pub-a1bf3067219c40438a869f85033c0815.r2.dev/restaurants/2/dishes/2.jpg', FALSE),
(2, 'Lau Sua Bo', 'Milk hotpot with beef', 215000, 'https://pub-a1bf3067219c40438a869f85033c0815.r2.dev/restaurants/2/dishes/3.jpg', FALSE),
(2, 'Lau Nam Bo', 'Mushroom hotpot with beef', 205000, 'https://pub-a1bf3067219c40438a869f85033c0815.r2.dev/restaurants/2/dishes/4.jpg', FALSE),
(2, 'Lau Nhat Bo', 'Japanese hotpot with beef', 219000, 'https://pub-a1bf3067219c40438a869f85033c0815.r2.dev/restaurants/2/dishes/5.jpg', FALSE),

(3, 'Kem Tran Chau Oreo', 'Ice cream with pearl and oreo', 20000, 'https://pub-a1bf3067219c40438a869f85033c0815.r2.dev/restaurants/3/dishes/1.jpg', FALSE),
(3, 'Kem Nhiet Doi', 'Tropical ice cream', 18000, 'https://pub-a1bf3067219c40438a869f85033c0815.r2.dev/restaurants/3/dishes/2.jpg', FALSE),
(3, 'Kem Matcha', 'Matcha green tea ice cream', 18000, 'https://pub-a1bf3067219c40438a869f85033c0815.r2.dev/restaurants/3/dishes/3.jpg', FALSE),
(3, 'Tra Dao Cam Sa', 'Peach oolong tea', 22000, 'https://pub-a1bf3067219c40438a869f85033c0815.r2.dev/restaurants/3/dishes/4.jpg', FALSE),

(4, 'Pho Tai', 'Rare beef pho', 55000, 'https://pub-a1bf3067219c40438a869f85033c0815.r2.dev/restaurants/4/dishes/1.jpg', FALSE),
(4, 'Pho Chin', 'Well-done beef pho', 52000, 'https://pub-a1bf3067219c40438a869f85033c0815.r2.dev/restaurants/4/dishes/2.jpg', FALSE),
(4, 'Pho Gan', 'Tendon pho', 60000, 'https://pub-a1bf3067219c40438a869f85033c0815.r2.dev/restaurants/4/dishes/3.jpg', FALSE),
(4, 'Com Bo Luc Lac', 'Shaking beef rice', 75000, 'https://pub-a1bf3067219c40438a869f85033c0815.r2.dev/restaurants/4/dishes/4.jpg', FALSE),
(4, 'Nuoc Mia', 'Sugarcane juice', 15000, 'https://pub-a1bf3067219c40438a869f85033c0815.r2.dev/restaurants/4/dishes/5.jpg', FALSE);

INSERT INTO restaurant_audio (poiid, languagecode, audiourl) VALUES
(1, 'vi', 'https://pub-a1bf3067219c40438a869f85033c0815.r2.dev/restaurants/1/audio/vi.wav'),
(1, 'en', 'https://pub-a1bf3067219c40438a869f85033c0815.r2.dev/restaurants/1/audio/en.wav'),
(1, 'cn', 'https://pub-a1bf3067219c40438a869f85033c0815.r2.dev/restaurants/1/audio/cn.wav'),
(1, 'ja', 'https://pub-a1bf3067219c40438a869f85033c0815.r2.dev/restaurants/1/audio/ja.wav'),
(1, 'ko', 'https://pub-a1bf3067219c40438a869f85033c0815.r2.dev/restaurants/1/audio/ko.wav'),
(2, 'vi', 'https://pub-a1bf3067219c40438a869f85033c0815.r2.dev/restaurants/2/audio/vi.wav'),
(2, 'en', 'https://pub-a1bf3067219c40438a869f85033c0815.r2.dev/restaurants/2/audio/en.wav'),
(2, 'cn', 'https://pub-a1bf3067219c40438a869f85033c0815.r2.dev/restaurants/2/audio/cn.wav'),
(2, 'ja', 'https://pub-a1bf3067219c40438a869f85033c0815.r2.dev/restaurants/2/audio/ja.wav'),
(2, 'ko', 'https://pub-a1bf3067219c40438a869f85033c0815.r2.dev/restaurants/2/audio/ko.wav'),
(3, 'vi', 'https://pub-a1bf3067219c40438a869f85033c0815.r2.dev/restaurants/3/audio/vi.wav'),
(3, 'en', 'https://pub-a1bf3067219c40438a869f85033c0815.r2.dev/restaurants/3/audio/en.wav'),
(3, 'cn', 'https://pub-a1bf3067219c40438a869f85033c0815.r2.dev/restaurants/3/audio/cn.wav'),
(3, 'ja', 'https://pub-a1bf3067219c40438a869f85033c0815.r2.dev/restaurants/3/audio/ja.wav'),
(3, 'ko', 'https://pub-a1bf3067219c40438a869f85033c0815.r2.dev/restaurants/3/audio/ko.wav'),
(4, 'vi', 'https://pub-a1bf3067219c40438a869f85033c0815.r2.dev/restaurants/4/audio/vi.wav'),
(4, 'en', 'https://pub-a1bf3067219c40438a869f85033c0815.r2.dev/restaurants/4/audio/en.wav'),
(4, 'cn', 'https://pub-a1bf3067219c40438a869f85033c0815.r2.dev/restaurants/4/audio/cn.wav'),
(4, 'ja', 'https://pub-a1bf3067219c40438a869f85033c0815.r2.dev/restaurants/4/audio/ja.wav'),
(4, 'ko', 'https://pub-a1bf3067219c40438a869f85033c0815.r2.dev/restaurants/4/audio/ko.wav');

-- Một yêu cầu script chờ duyệt (demo trang Admin)
INSERT INTO script_change_requests (poiid, languagecode, newscript, status, createdby) VALUES
(4, 'vi', 'Bản script mới: nhấn mạnh nước dù ninh 12 tiếng và bánh phở tráng tay mỗi ngày.', 'pending', 5);
