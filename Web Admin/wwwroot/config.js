/**
 * Tự chọn URL API: Admin HTTPS → gọi API HTTPS (tránh lỗi "Failed to fetch" do mixed content).
 * Có thể gán sẵn window.STREETFOOD_API trước khi nạp file này để ghi đè.
 */
(function () {
    if (typeof window.STREETFOOD_API === 'string' && window.STREETFOOD_API.length > 0) return;
    var https = typeof window !== 'undefined' && window.location && window.location.protocol === 'https:';
    window.STREETFOOD_API = https ? 'https://localhost:7288' : 'http://localhost:5288';
})();
window.STREETFOOD_ADMIN_KEY = window.STREETFOOD_ADMIN_KEY || 'streetfood-admin-dev-key-change-me';
