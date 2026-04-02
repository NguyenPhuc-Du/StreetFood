window.VendorLogout = function () {
    try {
        localStorage.removeItem('userRole');
        sessionStorage.removeItem('vendorUsername');
        sessionStorage.removeItem('vendorPassword');
    } catch (e) {
        // ignore
    }

    // Clear shared cookies so vendor creds don't survive cross-port redirect.
    try {
        document.cookie = 'vendorUsername=; path=/; expires=Thu, 01 Jan 1970 00:00:00 GMT; SameSite=Lax';
        document.cookie = 'vendorPassword=; path=/; expires=Thu, 01 Jan 1970 00:00:00 GMT; SameSite=Lax';
    } catch (e2) {
        // ignore
    }
    window.location.href = 'https://localhost:7238/html/loginPage.html';
};

