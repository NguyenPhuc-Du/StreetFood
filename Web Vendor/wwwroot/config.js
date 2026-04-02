/**
 * Tự chọn URL API theo protocol hiện tại (tránh mixed-content).
 * Có thể ghi đè bằng cách set sẵn window.STREETFOOD_API.
 */
(function () {
    if (typeof window.STREETFOOD_API === 'string' && window.STREETFOOD_API.length > 0) return;
    var https = typeof window !== 'undefined' && window.location && window.location.protocol === 'https:';
    window.STREETFOOD_API = https ? 'https://localhost:7236' : 'http://localhost:5191';

    // Used for role-based redirect after login (cross-origin between admin/vendor ports).
    var proto = https ? 'https' : 'http';
    window.STREETFOOD_ADMIN_DASHBOARD_URL = proto + '://localhost:7238/html/dashboardPage.html';
    window.STREETFOOD_VENDOR_DASHBOARD_URL = proto + '://localhost:7240/html/dashboardShopPage.html';
})();

