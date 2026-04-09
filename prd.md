# PRD (Product Requirements Document) - StreetFood


| Thuộc tính             | Giá trị                                                                                                                     |
| ---------------------- | --------------------------------------------------------------------------------------------------------------------------- |
| **Phiên bản tài liệu** | **2.2**                                                                                                                     |
| **Ngày cập nhật**      | **2026-04-08**                                                                                                              |
| **Trạng thái**         | Đồng bộ với mã nguồn trong repo (MVP vận hành được)                                                                         |
| **Mục đích**           | Mô tả yêu cầu sản phẩm; ghi nhận **tiến độ thực tế**, **phiên bản công nghệ**, **tham chiếu file** để nộp đồ án / bàn giao. |


### Mục lục nhanh

1. [Giới thiệu](#1-giới-thiệu) · 2. [Mục tiêu sản phẩm](#2-mục-tiêu-sản-phẩm) · 3. [Personas](#3-personas-và-nhu-cầu) · 4. [Tính năng chi tiết](#4-tính-năng-và-yêu-cầu-chức-năng) · 5–7. Stories / Luồng / Kiến trúc · 8. [CSDL](#8-tổng-quan-cơ-sở-dữ-liệu-database-overview) · 9–15. Analytics / Diagrams / UI · 16. [API thực tế](#16-api-overview-đã-triển-khai-trong-repo) · 17. Bảo mật · 18. [Roadmap](#18-kế-hoạch-triển-khai-roadmap-và-trạng-thái) · 19. Nghiệm thu · 20. Future · **[21. Danh mục tài liệu & mã tham chiếu](#21-danh-mục-tài-liệu-và-tham-chiếu-mã-nguồn)** · **[22. Lịch sử phiên bản PRD](#22-lịch-sử-phiên-bản-prd)**

---

## 1. Giới thiệu

### 1.1 Mục tiêu tài liệu

Tài liệu này mô tả đầy đủ yêu cầu sản phẩm cho dự án **StreetFood** theo hướng MVP có thể triển khai thực tế cho đồ án đại học hoặc startup giai đoạn đầu.  
PRD tập trung vào nghiệp vụ cốt lõi: phát audio giới thiệu nhà hàng theo vị trí GPS, đa ngôn ngữ, và khả năng quản trị nội dung qua web.

### 1.2 Tổng quan dự án

StreetFood là hệ thống **mobile + backend + admin/vendor web**:

- Người dùng quét QR để cài app.
- App dùng GPS phát hiện nhà hàng lân cận (POI).
- Khi người dùng đi vào bán kính POI, app tự động phát audio theo ngôn ngữ thiết bị.
- Người dùng có thể **chọn bất kỳ POI trên bản đồ** (kể cả đang ở xa) để mở thông tin và **phát audio giới thiệu chủ động**, kèm **thanh tiến trình / tua** (seek) theo thời gian thực.
- Backend cung cấp API dữ liệu POI, món ăn, audio đa ngôn ngữ.
- Admin quản trị dữ liệu và theo dõi analytics.
- Vendor cập nhật thông tin nhà hàng và gửi yêu cầu đổi audio script chờ admin duyệt.

### 1.3 Phạm vi phiên bản

- **In scope (MVP):** định vị, hiển thị POI, auto play audio, **chọn POI trên map để nghe on-demand**, **player có thanh mốc thời gian + tua được**, đa ngôn ngữ, quản trị cơ bản POI/foods/audio, analytics cơ bản.
- **Out of scope:** thanh toán, đặt bàn, loyalty, AI recommendation nâng cao thời gian thực.

### 1.4 Sản phẩm con & phiên bản công nghệ (repo hiện tại)


| Thành phần          | Vị trí trong repo                                                          | Công nghệ / phiên bản (tham chiếu `.csproj`)                                                                                                                               |
| ------------------- | -------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Mobile**          | `App/`                                                                     | .NET **MAUI** `net10.0` (Android, iOS, Mac Catalyst, Windows); `Microsoft.Maui.Controls` **10.0.50**; Maps, MediaElement, ZXing (QR), SQLite (`sqlite-net-pcl`).           |
| **API**             | `StreetFoodAPI/`                                                           | ASP.NET Core **net10.0**; **PostgreSQL** qua **Dapper** + `Npgsql`; Azure **Speech** (TTS) & **Translator** (dịch script); tùy chọn `Admin:ApiKey` + header `X-Admin-Key`. |
| **Admin Web**       | `Web Admin/wwwroot/`                                                       | ASP.NET Core static host **net10.0**; HTML/CSS/JS (`admin-api.js`, `config.js`).                                                                                           |
| **Vendor Web**      | `Web Vendor/wwwroot/`                                                      | Cùng kiểu static host **net10.0**.                                                                                                                                         |
| **Tầng dùng chung** | `StreetFood.Domain`, `StreetFood.Infrastructure` | Hỗ trợ API & migration SQL (`StreetFood.Infrastructure/Migrations`).                                                                                                       |


### 1.5 Cổng dịch vụ phát triển (launchSettings — tham chiếu)


| Dịch vụ           | HTTPS (mặc định dev)     | HTTP                                                          | Ghi chú                                                                                                          |
| ----------------- | ------------------------ | ------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------- |
| **StreetFoodAPI** | `https://localhost:7236` | `http://localhost:5191` (profile `http` có thể bind `*:5191`) | Client admin/vendor: biến `STREETFOOD_API` trong `Web Admin/wwwroot/config.js` / `Web Vendor/wwwroot/config.js`. |
| **Web Admin**     | `https://localhost:7238` | `http://localhost:5238`                                       | Trang quản trị: `/html/loginPage.html`, dashboard, heatmap, v.v.                                                 |
| **Web Vendor**    | `https://localhost:7240` | `http://localhost:5240`                                       | Cổng chủ quán.                                                                                                   |


**Lưu ý triển khai:** Cần chạy **API** và web cùng lúc; HTTPS tránh lỗi mixed content khi admin gọi API HTTPS.

---

## 2. Mục tiêu sản phẩm

### 2.1 Mục tiêu kinh doanh

- Tăng khả năng thu hút khách vãng lai cho nhà hàng.
- Tạo khác biệt bằng trải nghiệm audio theo vị trí.
- Xây nền tảng dữ liệu hành vi di chuyển để phục vụ quyết định vận hành.

### 2.2 Mục tiêu người dùng

- Tiếp cận nhanh thông tin nhà hàng gần nhất mà không cần tìm kiếm thủ công.
- Nghe nội dung giới thiệu bằng ngôn ngữ quen thuộc.
- Trải nghiệm mượt ngay cả khi mạng yếu (có cache offline).
- Nghe trước giới thiệu quán khi còn ở xa (ví dụ du khách lên kế hoạch) và điều chỉnh vị trí nghe trong file audio.

### 2.3 KPI đề xuất

- Tỷ lệ bật quyền vị trí: >= 80%.
- Tỷ lệ auto-trigger audio thành công trong vùng POI: >= 95%.
- P95 response time API đọc dữ liệu: < 300ms.
- Tỷ lệ fallback ngôn ngữ thành công: >= 99%.
- Thời lượng phiên dùng app trung bình: >= 3 phút.

---

## 3. Personas và nhu cầu

### 3.1 User (Khách/du khách)

- Cần biết chỗ ăn gần mình.
- Muốn thông tin nhanh, rảnh tay, đa ngôn ngữ.
- Không muốn thao tác nhiều khi đang di chuyển.

### 3.2 Vendor (Chủ nhà hàng)

- Muốn cập nhật thông tin nhà hàng, menu, hình ảnh.
- Muốn thay đổi nội dung audio nhưng qua quy trình kiểm duyệt.

### 3.3 Admin

- Quản lý hệ thống nội dung tập trung.
- Duyệt yêu cầu thay đổi script từ vendor.
- Theo dõi số liệu truy cập, heatmap, tuyến đường phổ biến.

### 3.4 Tiến độ triển khai (snapshot — đối chiếu mã nguồn)

Trạng thái gợi ý: **Đã có** = có UI/API rõ trong repo; **Một phần** = có nền tảng nhưng chưa đầy đủ so PRD gốc; **Tùy cấu hình** = cần khóa API/môi trường.

#### App MAUI (`App/Views/`)


| Mã     | Yêu cầu                                                   | Trạng thái   | Ghi chú                                                                           |
| ------ | --------------------------------------------------------- | ------------ | --------------------------------------------------------------------------------- |
| FR-M01 | QR / cổng cài app                                         | **Đã có**    | `QrGatePage.xaml`                                                                 |
| FR-M02 | GPS                                                       | **Đã có**    | `HomePage` + quyền vị trí                                                         |
| FR-M03 | POI trên bản đồ, chạm xem thông tin                       | **Đã có**    | Maps + card POI                                                                   |
| FR-M04 | Auto audio theo bán kính + cooldown / không cắt on-demand | **Đã có**    | Logic geofence + ưu tiên phát chủ động (theo triển khai hiện tại)                 |
| FR-M05 | Đa ngôn ngữ `vi/en/cn/ja/ko`                              | **Đã có**    | API `Accept-Language`; fallback                                                   |
| FR-M06 | SQLite cache                                              | **Một phần** | Thư viện `sqlite-net-pcl` có trong project; độ phủ cache POI/audio tùy triển khai |
| FR-M07 | Tìm kiếm / lọc                                            | **Đã có**    | `SuggestPage` (khoảng cách, v.v.)                                                 |
| FR-M08 | On-demand nghe ngoài bán kính                             | **Đã có**    | Chọn POI → `PoiDetailPage`                                                        |
| FR-M09 | Timeline + seek                                           | **Đã có**    | `CommunityToolkit.Maui.MediaElement`                                              |
| —      | Gửi thống kê thời lượng nghe                              | **Đã có**    | `POST /api/analytics/poi-audio-listen` (`ListenAnalyticsController`)              |


#### Backend API (`StreetFoodAPI/Controllers/`)


| Mã         | Yêu cầu           | Trạng thái | Ghi chú                                                                  |
| ---------- | ----------------- | ---------- | ------------------------------------------------------------------------ |
| FR-B01–B02 | POI, foods        | **Đã có**  | `PoiController`; bảng `POIs`, `Foods`, …                                 |
| FR-B03     | Audio đa ngôn ngữ | **Đã có**  | `Restaurant_Audio`; URL tới CDN/R2 tùy cấu hình                          |
| FR-B04     | Translation       | **Đã có**  | `POI_Translations`                                                       |
| FR-B05     | Telemetry         | **Đã có**  | Visit/path/listen events (admin analytics + `ListenAnalyticsController`) |
| —          | Auth web          | **Đã có**  | `POST /api/auth/login` (admin/vendor)                                    |
| —          | Auth app          | **Đã có**  | `AuthController` partial (register/login/activate app, device)           |


#### Admin Web (`Web Admin/wwwroot/html/`)


| Mã PRD                       | Trạng thái       | Ghi chú                                                                                                                                                                                           |
| ---------------------------- | ---------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| FR-A01 CRUD đầy đủ qua UI    | **Một phần**     | Có **tạo POI + tài khoản chủ quán** (`createPoiOwnerPage.html`); không có bộ CRUD POI/foods riêng đầy đủ như wireframe 15.2 “tab Foods/Audio” cổ điển — dùng API + luồng vendor cho phần còn lại. |
| FR-A02 Upload audio          | **Tùy cấu hình** | TTS/regenerate qua API (`regenerate-audio`); vendor có thể gửi **gói 5 URL** (`submit-audio-bundle`).                                                                                             |
| FR-A03 Dashboard / analytics | **Đã có**        | `dashboardPage.html`, `routeHeatmapPage.html` (heatmap, paths, popular paths/chains), **Thời lượng nghe** `poiListenStatsPage.html`.                                                              |
| FR-A04 Duyệt script          | **Đã có**        | `pendingScriptsPage.html`; phê duyệt + TTS/dịch theo `AdminController`.                                                                                                                           |


#### Vendor Web (`Web Vendor/wwwroot/html/`)


| Mã                            | Trạng thái | Ghi chú                                                          |
| ----------------------------- | ---------- | ---------------------------------------------------------------- |
| FR-V01 Sửa shop / menu        | **Đã có**  | `VendorShopController` (foods CRUD, update details)              |
| FR-V02 Request script / audio | **Đã có**  | `VendorScriptController`: `submit-script`, `submit-audio-bundle` |


---

## 4. Tính năng và yêu cầu chức năng

## 4.1 Mobile App (.NET MAUI)

### FR-M01: Cài đặt qua QR

- Người dùng quét QR để mở landing/install flow.
- Hỗ trợ deep link tới trang cài app tương ứng nền tảng.

### FR-M02: Định vị GPS

- Xin quyền vị trí khi mở app lần đầu.
- Theo dõi vị trí định kỳ khi app foreground.

### FR-M03: Hiển thị POI trên bản đồ

- Hiển thị marker cho nhà hàng lân cận (và/hoặc toàn bộ POI trong vùng tải dữ liệu).
- **Bấm marker / chạm POI** mở thẻ thông tin (bottom sheet hoặc card) **không phụ thuộc khoảng cách** — du khách có thể xem quán và quyết định nghe dù đang ở xa.

### FR-M04: Auto trigger audio theo bán kính POI

- Khi user vào bán kính POI, tự động hiển thị info card và phát audio.
- Có cooldown để tránh phát lặp quá dày.
- Khi ra khỏi vùng POI, dừng/ẩn trạng thái phát tự động.
- **Tương tác với chế độ chủ động (FR-M08):** Khi user đang phát audio do **chọn POI thủ công**, hệ thống **không tự đổi bài** sang POI khác chỉ vì geofence (tránh cắt ngang trải nghiệm). Auto-play geofence chỉ kích hoạt khi không có phiên phát on-demand đang active, hoặc sau khi user dừng/hoàn tất bài đang nghe — chi tiết ưu tiên do UX quyết định trong spec kỹ thuật.

### FR-M05: Đa ngôn ngữ

- Hỗ trợ `vi`, `en`, `zh`, `ja`, `ko`.
- Gửi `Accept-Language` theo ngôn ngữ thiết bị.
- Fallback về `en` nếu thiếu dữ liệu ngôn ngữ yêu cầu.

### FR-M06: Offline support (SQLite cache)

- Cache POI + translations + metadata gần nhất.
- Nếu offline: dùng dữ liệu cache để hiển thị bản đồ và thông tin cơ bản.
- Audio offline là tùy chọn nâng cao (phase sau); MVP ưu tiên stream.

### FR-M07: Tìm kiếm và lọc

- Tìm theo tên nhà hàng.
- Lọc theo loại món/khung giờ mở cửa/khoảng cách.

### FR-M08: Chọn POI trên bản đồ để nghe on-demand (không cần vào bán kính)

- Từ bản đồ hoặc kết quả tìm kiếm, user chọn một POI → hiển thị thông tin quán (tên, địa chỉ, giờ mở cửa, ảnh, khoảng cách nếu có GPS).
- Nút **“Nghe giới thiệu”** (hoặc tương đương) phát `AudioUrl` đúng ngôn ngữ thiết bị (fallback như FR-M05).
- Cho phép đóng thẻ / chuyển POI khác; khi chuyển POI có thể dừng bài cũ hoặc hỏi xác nhận (tùy MVP, mặc định: dừng và load bài mới).

### FR-M09: Trình phát audio với thanh mốc thời gian (timeline) và tua (seek)

- Luôn hiển thị khi đang có audio: **thời gian đã phát / tổng thời lượng** (ví dụ `0:42 / 2:15`).
- **Thanh trượt (slider/scrubber)** đồng bộ với tiến độ phát; user **kéo để tua** tới vị trí mong muốn.
- Giữ **Play / Pause**; khi tạm dừng, thanh thời gian giữ nguyên vị trí.
- Xử lý trạng thái tải/buffering: hiển thị loading hoặc khóa slider tạm thời cho đến khi stream sẵn sàng (theo khả năng nền tảng MAUI / MediaElement).
- Áp dụng chung cho cả **phát tự động theo geofence** và **phát on-demand** (cùng một component player).
- **Ghi chú triển khai:** Để seek ổn định, file audio trên R2/CDN nên phục vụ qua **HTTP Range requests** (hoặc định dạng phù hợp với control phát của MAUI); team kỹ thuật xác nhận trong spike trước khi khóa UI.

## 4.2 Backend API (.NET)

### FR-B01: Quản lý POI

- CRUD POI: tọa độ, bán kính, địa chỉ, ảnh.
- API đọc danh sách POI theo vùng địa lý hoặc gần user.

### FR-B02: Quản lý foods và images

- CRUD món ăn theo nhà hàng.
- Lưu URL ảnh món ăn.

### FR-B03: Quản lý audio đa ngôn ngữ

- Lưu metadata audio theo `PoiId + LanguageCode`.
- File audio lưu Cloudflare R2, DB lưu `AudioUrl`.

### FR-B04: Quản lý nội dung đa ngôn ngữ

- CRUD `POI_Translations`.
- Trả dữ liệu theo ngôn ngữ client.

### FR-B05: API cho mobile

- API đọc POI + translation + detail + audio.
- API tìm kiếm/lọc.
- API gửi telemetry visit/location/path.

## 4.3 Admin Web

### FR-A01: Quản lý nhà hàng

- CRUD POI, translations, foods, details.

### FR-A02: Quản lý audio

- Upload/replace audio lên R2.
- Gắn audio theo từng ngôn ngữ.

### FR-A03: Dashboard analytics

- Most visited restaurants.
- Heatmap vị trí người dùng.
- Most popular routes giữa POIs.
- Average visit duration.

### FR-A04: Duyệt yêu cầu từ vendor

- Danh sách request đổi script.
- Approve/Reject + lưu lịch sử.

**Triển khai UI (tham chiếu):** `Web Admin/wwwroot/html/` — `loginPage.html`, `dashboardPage.html`, `routeHeatmapPage.html`, `poiListenStatsPage.html`, `createPoiOwnerPage.html`, `pendingScriptsPage.html`, `restaurantOwnersPage.html`. Sidebar menu được **đồng bộ bằng** `admin-api.js` (inject DOM).

## 4.4 Vendor Web

### FR-V01: Chỉnh sửa thông tin nhà hàng

- Cập nhật profile nhà hàng và menu trong phạm vi được cấp quyền.

### FR-V02: Request thay đổi audio script

- Gửi text script mới theo ngôn ngữ.
- Không được tự thay audio trực tiếp.
- Theo dõi trạng thái: pending/approved/rejected.

---

## 5. User stories

### 5.1 User

- Là người dùng, tôi muốn thấy nhà hàng gần tôi trên bản đồ để quyết định ghé nhanh.
- Là người dùng, tôi muốn khi đi vào vùng POI thì audio tự phát để không cần thao tác.
- Là người dùng, tôi muốn nghe đúng ngôn ngữ máy của tôi.
- Là người dùng, tôi muốn app vẫn dùng được khi mạng yếu nhờ cache.
- Là người dùng, tôi muốn tìm và lọc nhà hàng theo nhu cầu.
- Là người dùng, tôi muốn **chạm vào POI trên bản đồ** để xem quán và **nghe giới thiệu ngay cả khi tôi chưa tới gần**.
- Là người dùng, tôi muốn **thấy thanh thời gian** của bài audio và **kéo tua** tới đoạn tôi muốn nghe lại hoặc bỏ qua phần đầu.

### 5.2 Vendor

- Là vendor, tôi muốn chỉnh thông tin nhà hàng của mình để thông tin luôn chính xác.
- Là vendor, tôi muốn gửi yêu cầu đổi script audio và chờ admin duyệt.

### 5.3 Admin

- Là admin, tôi muốn quản lý toàn bộ nhà hàng/món/audio trên một dashboard.
- Là admin, tôi muốn xem analytics để tối ưu vận hành.
- Là admin, tôi muốn duyệt yêu cầu vendor theo quy trình rõ ràng.

---

## 6. Luồng người dùng chính

### 6.1 User flow

**Luồng theo vị trí (geofence):**  
`Scan QR -> Install App -> Open App -> Grant GPS -> View Map/POIs -> Enter POI Radius -> Auto Play Audio (+ timeline / seek)`

**Luồng chủ động (du khách / nghe từ xa):**  
`Open App -> View Map/POIs -> Tap POI -> Xem thông tin -> Nghe giới thiệu (Play) -> Tùy chỉnh bằng thanh thời gian (tua) / Pause`

### 6.2 Vendor flow

`Login -> Edit Restaurant Info -> Submit Audio Script Request -> Wait Approval`

### 6.3 Admin flow

`Login -> Manage Data -> Review Vendor Requests -> Approve/Reject -> View Analytics`

---

## 7. Kiến trúc hệ thống (System Architecture Overview)

```mermaid
flowchart LR
    U[User Mobile App - MAUI] -->|HTTPS REST| API[StreetFood API - ASP.NET Core]
    V[Vendor Web] -->|HTTPS REST| API
    A[Admin Web] -->|HTTPS REST| API

    API --> DB[(PostgreSQL)]
    API --> R2[(Cloudflare R2 - Audio/Image)]
    U --> SQ[(SQLite Cache - Offline)]

    subgraph Analytics
      DB --> D1[Device_Visits]
      DB --> D2[Location_Logs]
      DB --> D3[Movement_Paths]
    end
```



---

## 8. Tổng quan cơ sở dữ liệu (Database Overview)

## 8.1 Danh sách bảng chính

- `POIs`
- `POI_Translations`
- `Restaurant_Audio`
- `Restaurant_Details`
- `Foods`
- `Users`
- `Restaurant_Owners`
- `Script_Change_Requests`
- `Device_Visits`
- `Location_Logs`
- `Movement_Paths`
- `Languages`
- `poi_audio_listen_events` (thống kê thời lượng nghe từ app — xem `ListenAnalyticsController`)
- `schema_migrations` (lịch sử file SQL đã chạy — do `DbInitializer` tạo)

## 8.2 Sơ đồ ERD (ảnh)

Ảnh sơ đồ quan hệ thực thể nằm trong repo tại `Public/Resouces/ERD.png` (thư mục giữ tên theo cấu trúc hiện có).

![Sơ đồ ERD — StreetFood (các bảng và quan hệ)](Public/Resouces/ERD.png)

*Hình 1 — ERD tổng quan: POI, người dùng, audio đa ngôn ngữ, analytics và các bảng phụ trợ. Đối chiếu thêm danh sách bảng đầy đủ tại [mục 8.1](#81-danh-sách-bảng-chính) và migration `V1__Initial_schema.sql` nếu schema đã mở rộng so với sơ đồ.*

---

## 9. Thiết kế analytics

### 9.1 Số liệu cần thu thập

- **Visit count theo POI:** từ `Device_Visits`.
- **Visit duration:** `ExitTime - EnterTime`.
- **Heatmap:** tổng hợp `Location_Logs` theo lưới tọa độ.
- **Popular routes:** chuỗi chuyển dịch `FromPoiId -> ToPoiId` từ `Movement_Paths`.

### 9.2 Metrics/dashboard

- Top N POI theo lượt ghé.
- Thời lượng ghé trung bình theo POI.
- Bản đồ nhiệt theo khung giờ.
- Top tuyến di chuyển (cặp POI) theo tần suất.

### 9.3 Chu kỳ cập nhật

- Near real-time (1-5 phút) cho dashboard vận hành.
- Daily aggregation cho báo cáo xu hướng.

---

## 10. Yêu cầu phi chức năng (NFR)

- **Hiệu năng:** API đọc dữ liệu P95 < 300ms.
- **Khả năng mở rộng:** hỗ trợ tăng số lượng POI và người dùng đồng thời.
- **Bảo mật:** JWT authentication, RBAC (admin/vendor), mã hóa kết nối HTTPS.
- **Độ tin cậy:** hệ thống hoạt động ổn định khi mạng chập chờn.
- **Offline:** mobile có SQLite cache cho dữ liệu đọc.
- **Khả dụng:** UX đơn giản, ít thao tác, phản hồi rõ khi lỗi mạng/quyền vị trí.

---

## 11. Sơ đồ Use Case

```mermaid
flowchart TB
    User((User))
    Vendor((Vendor))
    Admin((Admin))

    UC1[Quét QR và cài app]
    UC2[Xem POI gần đây]
    UC3[Nghe audio tự động]
    UC4[Tìm kiếm và lọc]
    UC4b[Chọn POI trên bản đồ để nghe chủ động]
    UC4c[Điều khiển timeline và tua audio]

    UC6[Chỉnh sửa thông tin nhà hàng]

    UC8[Đăng nhập Admin]
    UC9[CRUD nha hang/foods/audio]
    UC10[Duyệt yêu cầu vendor]
    UC11[Xem analytics dashboard]
    UC12[Quản lý tài khoản owner]
    UC13[Tạo lại audio theo script đã duyệt]
    UC14[Theo dõi heatmap và movement]
    UC15[Đăng nhập Vendor]
    UC16[Quản lý món ăn của cửa hàng]
    UC17[Gửi script hoặc audio bundle cho Admin]
    UC18[Quét JWT QR kích hoạt app local]
    UC19[Theo dõi geofence và log analytics]

    User --> UC1
    User --> UC18
    User --> UC2
    User --> UC3
    User --> UC4
    User --> UC4b
    User --> UC4c
    User --> UC19

    Vendor --> UC15
    Vendor --> UC6
    Vendor --> UC16
    Vendor --> UC17

    Admin --> UC8
    Admin --> UC9
    Admin --> UC10
    Admin --> UC11
    Admin --> UC12
    Admin --> UC13
    Admin --> UC14
```



---

## 12. Sequence diagram (User vào POI và phát audio)

```mermaid
sequenceDiagram
    participant U as User
    participant M as Mobile App (MAUI)
    participant API as Backend API
    participant DB as PostgreSQL
    participant R2 as Cloudflare R2

    U->>M: Mở app + cấp quyền GPS
    M->>API: GET /api/poi (Accept-Language)
    API->>DB: Query POI + translation + audio
    DB-->>API: Data POI
    API-->>M: Danh sách POI

    loop Mỗi chu kỳ GPS
      M->>M: Đọc vị trí hiện tại
      M->>M: Tính khoảng cách đến POI
    end

    M->>M: User vào bán kính POI
    M->>R2: Stream audio theo AudioUrl
    R2-->>M: Audio content
    M-->>U: Tự động phát audio + hiện info card
```



## 12.1 Sequence diagram (User chọn POI trên map — on-demand + timeline)

```mermaid
sequenceDiagram
    participant U as User
    participant M as Mobile App (MAUI)
    participant R2 as Cloudflare R2

    U->>M: Chạm vào marker POI trên bản đồ
    M-->>U: Hiện thông tin quán + nút Nghe giới thiệu
    U->>M: Bấm Nghe / Play
    M->>M: Load AudioUrl (ngôn ngữ thiết bị)
    M->>R2: Stream audio
    R2-->>M: Audio stream
    M-->>U: Phát audio + hiện timeline (current/total)
    U->>M: Kéo slider / seek
    M->>M: Seek đến vị trí mới
    M-->>U: Tiếp tục phát từ vị trí đã chọn
```

## 12.2 Sequence diagram (Vendor gửi script/audio -> Admin duyệt -> App nhận dữ liệu mới)

```mermaid
sequenceDiagram
    participant V as Vendor Web
    participant API as StreetFood API
    participant DB as PostgreSQL
    participant A as Admin Web
    participant TTS as Azure Speech/Translator
    participant R2 as Cloudflare R2
    participant M as Mobile App

    V->>API: POST /api/vendor/submit-script (hoặc submit-audio-bundle)
    API->>DB: Insert script_change_requests (pending)
    API-->>V: 200 Pending

    A->>API: GET /api/Admin/script-requests/pending
    API-->>A: Danh sách request
    A->>API: POST /api/Admin/script-requests/{id}/approve

    alt Submit script text
        API->>TTS: Translate + synthesize audio
        TTS-->>API: Audio files
        API->>R2: Upload audio files
        R2-->>API: Public URLs
        API->>DB: Update restaurant_audio + translations + status approved
    else Submit audio bundle URLs
        API->>DB: Update restaurant_audio + status approved
    end

    M->>API: GET /api/poi/{id}
    API->>DB: Query detail + restaurant_audio
    DB-->>API: Dữ liệu mới nhất
    API-->>M: audioUrl/description đã cập nhật
```

## 12.3 Sequence diagram (Telemetry: location -> visit -> movement -> dashboard)

```mermaid
sequenceDiagram
    participant M as Mobile App
    participant API as StreetFood API
    participant DB as PostgreSQL
    participant A as Admin Web

    loop Mỗi chu kỳ vị trí
      M->>API: POST /api/poi/log (deviceId, lat, lng)
      API->>DB: INSERT location_logs
    end

    M->>API: POST /api/poi/visit/start
    API->>DB: INSERT device_visits (entertime)
    M->>API: POST /api/poi/movement (fromPoiId,toPoiId)
    API->>DB: INSERT movement_paths
    M->>API: POST /api/poi/visit/end
    API->>DB: UPDATE device_visits (exittime,duration)

    A->>API: GET /api/Admin/analytics/heatmap
    A->>API: GET /api/Admin/analytics/paths
    A->>API: GET /api/Admin/analytics/popular-paths
    API->>DB: Aggregate queries
    DB-->>API: Result sets
    API-->>A: Dashboard data
```

## 12.4 Sequence diagram (Admin tạo POI + owner, Vendor quản lý món, App hiển thị)

```mermaid
sequenceDiagram
    participant A as Admin Web
    participant API as StreetFood API
    participant DB as PostgreSQL
    participant V as Vendor Web
    participant M as Mobile App

    A->>API: POST /api/Admin/poi-with-owner
    API->>DB: Insert users(role=vendor), pois, owners
    DB-->>API: Tạo thành công
    API-->>A: 200 + thông tin tài khoản owner

    V->>API: POST /api/vendor/foods/create
    API->>DB: Insert foods theo PoiId của vendor
    API-->>V: 200 Created
    V->>API: POST /api/vendor/foods/update
    API->>DB: Update foods
    API-->>V: 200 Updated

    M->>API: GET /api/poi/{id}
    API->>DB: Query poi + foods + details
    DB-->>API: Dữ liệu đầy đủ
    API-->>M: Chi tiết quán + danh sách món
```

## 12.5 Sequence diagram (App kích hoạt JWT local 7 ngày)

```mermaid
sequenceDiagram
    participant U as User
    participant M as Mobile App
    participant QR as QR JWT
    participant S as Local Storage (Preferences)

    U->>M: Mở màn quét QR
    M->>QR: Đọc token JWT từ mã QR
    M->>M: Verify chữ ký + nbf/exp + issuer/type
    alt Token hợp lệ
        M->>S: Lưu hạn kích hoạt local (7 ngày)
        M-->>U: Vào HomePage
    else Token không hợp lệ/hết hạn
        M-->>U: Báo lỗi và yêu cầu quét lại
    end
```



---

## 13. Activity diagram (Vendor request đổi script)

```mermaid
flowchart TD
    S[Vendor đăng nhập] --> A[Mở trang quản lý nhà hàng]
    A --> B[Nhập script mới]
    B --> C[Submit request]
    C --> D[Hệ thống lưu Script_Change_Request = Pending]
    D --> E[Admin nhận request]
    E --> F{Phê duyệt?}
    F -- Yes --> G[Upload/cập nhật audio]
    G --> H[Set request = Approved]
    F -- No --> I[Set request = Rejected]
    H --> J[Thông báo kết quả cho Vendor]
    I --> J
```

## 13.1 Activity diagram (User App end-to-end)

```mermaid
flowchart TD
    A0[Mở app] --> A1{Đã kích hoạt?}
    A1 -- Chưa --> A2[Quét JWT QR]
    A2 --> A3[Lưu hạn local 7 ngày]
    A1 -- Đã kích hoạt --> A4[Xin quyền GPS]
    A3 --> A4
    A4 --> A5[Load POI từ API]
    A5 --> A6[Hiện map + marker]
    A6 --> A7{Người dùng thao tác}
    A7 -- Chạm POI --> A8[Mở thẻ thông tin/chi tiết]
    A8 --> A9[Play/Pause/Seek audio]
    A7 -- Đi vào geofence --> A10[Auto show card + play]
    A10 --> A9
    A7 -- Di chuyển --> A11[Gửi location_logs + visit start/end + movement]
    A11 --> A6
```

## 13.2 Activity diagram (Admin dashboard + moderation)

```mermaid
flowchart TD
    B0[Admin đăng nhập] --> B1[Mở dashboard]
    B1 --> B2[Đọc KPI + heatmap + routes + listen stats]
    B2 --> B3{Có request pending?}
    B3 -- Có --> B4[Mở pendingScripts]
    B4 --> B5{Approve hay Reject}
    B5 -- Approve --> B6[Cập nhật DB + audio URLs]
    B5 -- Reject --> B7[Set status rejected]
    B6 --> B8[Refresh dashboard]
    B7 --> B8
    B3 -- Không --> B8
```

## 13.3 Activity diagram (Vendor quản lý cửa hàng và món ăn)

```mermaid
flowchart TD
    V0[Vendor đăng nhập] --> V1[Lấy danh sách POI của mình]
    V1 --> V2[Chọn cửa hàng]
    V2 --> V3{Thao tác}
    V3 -- Cập nhật chi tiết --> V4[POST /api/vendor/shop/update-details]
    V3 -- Thêm món --> V5[POST /api/vendor/foods/create]
    V3 -- Sửa món --> V6[POST /api/vendor/foods/update]
    V3 -- Ẩn/Xóa món --> V7[POST /api/vendor/foods/delete]
    V4 --> V8[Reload dữ liệu]
    V5 --> V8
    V6 --> V8
    V7 --> V8
```

## 13.4 Activity diagram (Admin tạo POI + owner và giám sát analytics)

```mermaid
flowchart TD
    C0[Admin đăng nhập] --> C1[Tạo POI + owner]
    C1 --> C2[Hệ thống sinh user vendor + liên kết owner]
    C2 --> C3[Theo dõi owner list]
    C3 --> C4{Có vấn đề tài khoản?}
    C4 -- Có --> C5[Hide owner account]
    C4 -- Không --> C6[Giữ hoạt động bình thường]
    C5 --> C7[Xem dashboard analytics]
    C6 --> C7
    C7 --> C8[Đánh giá heatmap, routes, top POI]
```



---

## 14. Data Flow Diagram (DFD Level 1)

```mermaid
flowchart LR
    U[User App]
    V[Vendor Web]
    A[Admin Web]

    P1((P1: Quản lý POI/Nội dung))
    P2((P2: Xử lý audio đa ngôn ngữ))
    P3((P3: Tracking & Analytics))

    D1[(POI + Foods + Translations)]
    D2[(Audio Metadata)]
    D3[(Tracking Logs)]
    R2[(Cloudflare R2)]

    U -->|Yeu cau POI + gui GPS| P1
    U -->|Audio playback request| P2
    U -->|Telemetry| P3

    V -->|Cap nhat thong tin + request script| P1
    A -->|CRUD + Duyet request| P1
    A -->|Dashboard query| P3

    P1 <--> D1
    P1 <--> D2
    P2 <--> D2
    P2 <--> R2
    P3 <--> D3
```



---

## 15. UI wireframe (MVP)

## 15.1 Mobile - Home map

```text
+--------------------------------------------------+
| StreetFood                                       |
| [Search.................] [Filter]               |
|                                                  |
|                (MAP VIEW)                        |
|          o User                                  |
|      [POI1]   [POI2]   [POI3]                    |
|                                                  |
|----------------------------------------------    |
| [Restaurant Card]  (mo khi: gan POI HOAC cham POI) |
| Name: Highlands Coffee                           |
| Address: 98 Nguyen Tri Phuong                    |
| Open: 07:00 - 22:00   | ~1.2 km (neu co GPS)     |
| [Nghe gioi thieu]  [Play] [Pause]                |
| |------o-----------------------|  0:42 / 2:15     |
|  ^-------- slider timeline (tua duoc) ----------- |
+--------------------------------------------------+
```

*Ghi chú:* Dòng timeline hiển thị khi có audio; user kéo “o” để tua. Có thể gom “Nghe giới thiệu” với Play nếu UI tối giản.

## 15.2 Admin - Dashboard

```text
+---------------------------------------------------------------+
| Admin Dashboard                                               |
| [POIs] [Foods] [Audio] [Requests] [Analytics]                |
|---------------------------------------------------------------|
| KPI: Visits Today | Avg Duration | Active POIs               |
|---------------------------------------------------------------|
| Heatmap Panel                 | Popular Routes                |
|                               | POI A -> POI B (120)         |
|                               | POI B -> POI C (95)          |
|---------------------------------------------------------------|
| Most Visited Restaurants Table                                |
+---------------------------------------------------------------+
```

## 15.3 Vendor - Request audio script

```text
+--------------------------------------------------+
| Vendor Portal                                    |
| [Restaurant Info] [Menu] [Audio Requests]        |
|--------------------------------------------------|
| Restaurant: Co Chi Thi Nen                       |
| Language: [vi v]                                 |
| New Script:                                      |
| [............................................]   |
| [............................................]   |
| Status: Pending                                  |
| [Submit Request]                                 |
+--------------------------------------------------+
```

---

## 16. API overview (đã triển khai trong repo)

Base URL ví dụ: `https://localhost:7236`. Route gốc của controller nằm trong cột **Prefix** (ASP.NET Core `[Route]`).

### 16.1 Mobile & POI công khai (`PoiController` → `/api/Poi`)


| Phương thức | Đường dẫn       | Mô tả                                                |
| ----------- | --------------- | ---------------------------------------------------- |
| GET         | `/api/Poi`      | Danh sách POI + audio theo `Accept-Language`.        |
| GET         | `/api/Poi/{id}` | Chi tiết POI + foods (ẩn món `IsHidden` nếu có cột). |


### 16.2 Telemetry nghe audio (`ListenAnalyticsController` → `/api/analytics`)


| Phương thức | Đường dẫn                         | Mô tả                                                                           |
| ----------- | --------------------------------- | ------------------------------------------------------------------------------- |
| POST        | `/api/analytics/poi-audio-listen` | App gửi `poiId`, `durationSeconds`, `deviceId` (lưu `poi_audio_listen_events`). |


### 16.3 Xác thực (`AuthController` → `/api/Auth`)


| Phương thức | Đường dẫn         | Mô tả                                                                                                            |
| ----------- | ----------------- | ---------------------------------------------------------------------------------------------------------------- |
| POST        | `/api/Auth/login` | Đăng nhập admin/vendor (bảng `users`).                                                                           |
| POST        | `/api/Auth/...`   | Các endpoint app/device cũ còn trong controller (khuyến nghị cleanup để đồng bộ 100% với mô hình kích hoạt JWT local của app). |


### 16.4 Quản trị (`AdminController` → `/api/Admin`) — cần `X-Admin-Key` nếu cấu hình `Admin:ApiKey`


| Phương thức | Đường dẫn                                     | Mô tả                                                                    |
| ----------- | --------------------------------------------- | ------------------------------------------------------------------------ |
| GET         | `/api/Admin/dashboard/summary`                | KPI: số POI, vendor, pending script, audio tracks, mẫu location 30 ngày. |
| GET         | `/api/Admin/analytics/heatmap`                | Điểm nhiệt từ `location_logs`.                                           |
| GET         | `/api/Admin/analytics/poi-audio-listen?days=` | Thống kê **thời lượng nghe** trung bình theo POI.                        |
| GET         | `/api/Admin/analytics/paths`                  | Chuỗi di chuyển gần đây.                                                 |
| GET         | `/api/Admin/analytics/popular-paths`          | Cặp POI A→B phổ biến.                                                    |
| GET         | `/api/Admin/analytics/popular-route-chains`   | Chuỗi tối đa N POI.                                                      |
| POST        | `/api/Admin/poi-with-owner`                   | Tạo user vendor + POI + script khởi tạo (admin).                         |
| GET         | `/api/Admin/pois/awaiting-script`             | POI chờ script/vendor.                                                   |
| GET         | `/api/Admin/script-requests/pending`          | Yêu cầu script chờ duyệt.                                                |
| POST        | `/api/Admin/script-requests/{id}/approve`     | Duyệt + pipeline dịch/TTS (theo cấu hình).                               |
| POST        | `/api/Admin/poi/{poiId}/regenerate-audio`     | Tạo lại MP3 (Azure Speech).                                              |
| GET         | `/api/Admin/owners`                           | Danh sách chủ quán.                                                      |
| POST        | `/api/Admin/owners/{userId}/hide`             | Ẩn tài khoản vendor.                                                     |


### 16.5 Vendor (các controller dùng chung prefix `**/api/vendor`**)


| Endpoint (POST)                   | Mô tả                                            |
| --------------------------------- | ------------------------------------------------ |
| `/api/vendor/pois/list`           | Danh sách POI của vendor.                        |
| `/api/vendor/shop/update-details` | Cập nhật chi tiết cửa hàng.                      |
| `/api/vendor/foods/list`          | Danh sách món.                                   |
| `/api/vendor/foods/create`        | Tạo món.                                         |
| `/api/vendor/foods/update`        | Cập nhật món.                                    |
| `/api/vendor/foods/delete`        | Xóa/ẩn món.                                      |
| `/api/vendor/submit-script`       | Gửi script chờ duyệt (`VendorScriptController`). |
| `/api/vendor/submit-audio-bundle` | Gửi gói 5 URL audio (5 ngôn ngữ).                |
| `/api/vendor/media/upload`        | Upload ảnh (multipart).                          |


> **Ghi chú:** Danh sách trên lấy từ attribute `[HttpGet]`/`[HttpPost]` trong `StreetFoodAPI/Controllers/`. Một số đường dẫn REST “chuẩn CRUD” trong PRD 1.x (ví dụ `GET /api/poi/search`) **chưa** thấy triển khai riêng — tìm kiếm/lọc được đảm nhiệm phía app (`SuggestPage`) trên dữ liệu đã tải hoặc API hiện có.

---

## 17. Bảo mật và phân quyền

### 17.1 Mục tiêu dài hạn (PRD gốc)

- JWT / token cho phiên API; RBAC rõ `admin` / `vendor` / `user`.
- Audit log; rate limit.

### 17.2 Triển khai hiện tại (đối chiếu code)

- **Đăng nhập web:** `POST /api/Auth/login` kiểm tra `users` (role `admin` | `vendor`), không JWT trong repo tại thời điểm cập nhật PRD 2.0.
- **Admin API:** khóa tùy chọn qua `Admin:ApiKey` + header `**X-Admin-Key`** (`AdminController.IsAdmin`).
- **Khuyến nghị môi trường thật:** HTTPS, đổi khóa admin mặc định trong `config.js` / `appsettings`, bật CORS chặt theo origin.

---

## 18. Kế hoạch triển khai (Roadmap) và trạng thái


| Giai đoạn               | Nội dung                                                                           | Trạng thái (2026-04)                                                                         |
| ----------------------- | ---------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------- |
| **Phase 1 – MVP core**  | App: bản đồ, POI, geofence, on-demand, player, đa ngôn ngữ; API đọc POI/detail; DB | **Đã đạt** cốt lõi trong repo.                                                               |
| **Phase 2 – Vận hành**  | Admin tạo POI+owner, vendor cập nhật shop/foods, workflow script/audio, TTS/Azure  | **Đã đạt** theo luồng hiện có; CRUD admin “full UI” như wireframe cổ điển **chưa** bắt buộc. |
| **Phase 3 – Analytics** | Heatmap, paths, popular routes/chains, **thời lượng nghe theo POI**                | **Đã đạt** trên dashboard + các trang admin tương ứng.                                       |
| **Phase 4 – Tối ưu**    | Offline sâu, geofence tiết kiệm pin, gợi ý cá nhân hóa                             | **Kế tiếp** / ngoài phạm vi MVP hiện tại.                                                    |


---

## 19. Tiêu chí nghiệm thu MVP

- User cài app qua QR và mở thành công.
- App hiển thị đúng POI gần vị trí hiện tại.
- Vào bán kính POI thì audio tự phát đúng ngôn ngữ.
- User **chạm POI trên bản đồ** khi **ngoài bán kính** vẫn mở được thông tin và **phát được audio** sau khi bấm nghe.
- Khi đang phát audio, UI có **thời gian đã phát / tổng thời lượng** và **thanh tua**; kéo seek cập nhật đúng vị trí phát (trong giới hạn hỗ trợ của nguồn stream).
- Quy tắc **không cắt ngang** bài on-demand bởi auto geofence được áp dụng như mô tả FR-M04.
- Admin **tạo được POI kèm chủ quán** và **theo dõi được** dashboard/heatmap/paths/thời lượng nghe; vendor **bổ sung** foods qua cổng vendor (đúng mô hình phân tách hiện tại).
- Vendor gửi được request script / gói audio, admin duyệt được.
- Dashboard hiển thị được các chỉ số tổng hợp (POI, vendor, pending script, audio tracks, mẫu location, v.v.).

---

## 20. Future improvements

- Hoàn thiện JWT/session đồng nhất cho API thay vì chỉ login trả payload (nếu cần scale bảo mật).
- CRUD POI/foods **thuần admin UI** nếu yêu cầu nghiệp vụ thay vì chủ yếu qua vendor + `poi-with-owner`.
- AI voice generation/TTS ngoài Azure (tùy chi phí).
- Recommendation engine theo hành vi di chuyển.
- A/B test nội dung audio để tối ưu chuyển đổi ghé quán.
- Tích hợp chiến dịch theo khung giờ và sự kiện địa phương.
- Mở rộng đa thành phố / multi-tenant.

---

## 21. Danh mục tài liệu và tham chiếu mã nguồn


| Loại                     | Đường dẫn / ghi chú                                                                                                                                                                                       |
| ------------------------ | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **PRD (tài liệu này)**   | `prd.md` (root repo)                                                                                                                                                                                      |
| **Cấu hình API cho web** | `Web Admin/wwwroot/config.js`, `Web Vendor/wwwroot/config.js`                                                                                                                                             |
| **Client gọi API admin** | `Web Admin/wwwroot/js/admin-api.js` (gồm inject sidebar StreetFood)                                                                                                                                       |
| **API**                  | `StreetFoodAPI/Controllers/*.cs`, `StreetFoodAPI/Program.cs`, `StreetFoodAPI/appsettings.json`                                                                                                            |
| **Migration / SQL**      | `StreetFood.Infrastructure/Migrations/` — **`V1__Initial_schema.sql`** (DDL đầy đủ), **`V2__Seed_core_data.sql`** (dữ liệu nền), **`V3__Seed_demo_analytics.sql`** (heatmap/tuyến/thống kê nghe). DB mới: tạo database trống rồi chạy lần lượt hoặc bật `DbInitializer` (nếu được kích hoạt trong `Program.cs`). |
| **App MAUI**             | `App/Views/*.xaml`, `App/AppShell.xaml`                                                                                                                                                                   |
| **Trang HTML admin**     | `Web Admin/wwwroot/html/` — `loginPage.html`, `dashboardPage.html`, `routeHeatmapPage.html`, `poiListenStatsPage.html`, `createPoiOwnerPage.html`, `pendingScriptsPage.html`, `restaurantOwnersPage.html` |
| **Trang HTML vendor**    | `Web Vendor/wwwroot/html/` — `dashboardShopPage.html`, `manageProductsPage.html`, `requestScriptPage.html`, …                                                                                             |
| **Sơ đồ ERD**            | File ảnh `Public/Resouces/ERD.png` — nhúng trong PRD tại [mục 8.2](#82-sơ-đồ-erd-ảnh).                                                                                                                    |


---

## 22. Lịch sử phiên bản PRD


| Phiên bản | Ngày            | Nội dung thay đổi chính                                                                                                                                                                             |
| --------- | --------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **1.0**   | (trước 2026-04) | PRD MVP: personas, FR mobile/backend/admin/vendor, diagram, API đề xuất, roadmap.                                                                                                                   |
| **2.0**   | **2026-04-06**  | Bổ sung: phiên bản stack & cổng dev; **bảng tiến độ thực tế**; **API đã triển khai**; chỉnh roadmap trạng thái; bảo mật đối chiếu code; **danh mục tài liệu / file tham chiếu**; lịch sử phiên bản. |
| **2.1**   | **2026-04-06**  | Mục **8.2**: nhúng ảnh **ERD** (`Public/Resouces/ERD.png`); cập nhật **8.1** (thêm `device_activations`, `schema_migrations`); chỉnh tiêu đề và ghi chú ERD. Metadata PRD **2.1**. |
| **2.2**   | **2026-04-08**  | Đồng bộ codebase hiện tại: bỏ `StreetFood.Application`; đồng bộ migration mới (loại `app_activation_expires_at` và `device_activations`); mở rộng dữ liệu demo analytics; bổ sung bộ **Use Case + Sequence + Activity** đầy đủ cho App, API, Web Admin, Web Vendor. |


