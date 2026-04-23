/**
 * Tự chọn URL API: Admin HTTPS → gọi API HTTPS (tránh lỗi "Failed to fetch" do mixed content).
 * Có thể gán sẵn window.STREETFOOD_API trước khi nạp file này để ghi đè.
 */
(function () {
    if (typeof window.STREETFOOD_API === 'string' && window.STREETFOOD_API.length > 0) return;
    var https = typeof window !== 'undefined' && window.location && window.location.protocol === 'https:';
    // Env-like override (inject before config.js): window.STREETFOOD_API_BASE_URL = 'https://...';
    var envApi = (typeof window.STREETFOOD_API_BASE_URL === 'string' ? window.STREETFOOD_API_BASE_URL : '').trim();
    window.STREETFOOD_API = envApi || 'https://flatly-creamer-bucket.ngrok-free.dev';

    // Used for role-based redirect after login (cross-origin between admin/vendor ports).
    var proto = https ? 'https' : 'http';
    window.STREETFOOD_ADMIN_DASHBOARD_URL = proto + '://localhost:7238/html/dashboardPage.html';
    window.STREETFOOD_VENDOR_DASHBOARD_URL = proto + '://localhost:7240/html/dashboardShopPage.html';
})();
window.STREETFOOD_ADMIN_KEY = window.STREETFOOD_ADMIN_KEY || 'streetfood-admin-dev-key-change-me';
/** Dự phòng nếu API chưa cấu hình GoogleMaps:ApiKey — nên dùng cùng key Maps (HTTP referrer) như app. */
window.STREETFOOD_GOOGLE_MAPS_KEY = window.STREETFOOD_GOOGLE_MAPS_KEY || '';
