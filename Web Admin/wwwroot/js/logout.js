window.AdminLogout = function () {
    try {
        localStorage.removeItem('userRole');
    } catch (e) {
        // ignore
    }

    // Also clear vendor shared cookies (login is shared between admin/vendor).
    try {
        document.cookie = 'vendorUsername=; path=/; expires=Thu, 01 Jan 1970 00:00:00 GMT; SameSite=Lax';
        document.cookie = 'vendorPassword=; path=/; expires=Thu, 01 Jan 1970 00:00:00 GMT; SameSite=Lax';
    } catch (e2) {
        // ignore
    }
    window.location.href = 'https://localhost:7238/html/loginPage.html';
};

