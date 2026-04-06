/**
 * Tự chọn URL API: Admin HTTPS → gọi API HTTPS (tránh lỗi "Failed to fetch" do mixed content).
 * Có thể gán sẵn window.STREETFOOD_API trước khi nạp file này để ghi đè.
 */
(function () {
    if (typeof window.STREETFOOD_API === 'string' && window.STREETFOOD_API.length > 0) return;
    var https = typeof window !== 'undefined' && window.location && window.location.protocol === 'https:';
    // Must match StreetFoodAPI launchSettings.json ports (dev).
    window.STREETFOOD_API = https ? 'https://localhost:7236' : 'http://localhost:5191';

    // Used for role-based redirect after login (cross-origin between admin/vendor ports).
    var proto = https ? 'https' : 'http';
    window.STREETFOOD_ADMIN_DASHBOARD_URL = proto + '://localhost:7238/html/dashboardPage.html';
    window.STREETFOOD_VENDOR_DASHBOARD_URL = proto + '://localhost:7240/html/dashboardShopPage.html';
})();
window.STREETFOOD_ADMIN_KEY = window.STREETFOOD_ADMIN_KEY || 'streetfood-admin-dev-key-change-me';
/** Dự phòng nếu API chưa cấu hình GoogleMaps:ApiKey — nên dùng cùng key Maps (HTTP referrer) như app. */
window.STREETFOOD_GOOGLE_MAPS_KEY = window.STREETFOOD_GOOGLE_MAPS_KEY || '';
