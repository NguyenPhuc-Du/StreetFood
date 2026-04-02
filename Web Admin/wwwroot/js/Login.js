(function () {
    const passwordInput = document.getElementById('txtPassword');
    const toggleBtn = document.getElementById('togglePasswordBtn');
    const eyeOpenIcon = document.getElementById('eyeOpenIcon');
    const eyeClosedIcon = document.getElementById('eyeClosedIcon');

    if (!passwordInput || !toggleBtn) return;

    function syncToggleIcon() {
        const isPasswordHidden = passwordInput.type === 'password';
        if (eyeOpenIcon && eyeClosedIcon) {
            eyeOpenIcon.classList.toggle('hidden', !isPasswordHidden);
            eyeClosedIcon.classList.toggle('hidden', isPasswordHidden);
        }
        toggleBtn.setAttribute('aria-label', isPasswordHidden ? 'Show password' : 'Hide password');
    }

    function syncToggleButtonVisibility() {
        // Only show toggle when user has typed something.
        toggleBtn.style.display = passwordInput.value ? 'inline-flex' : 'none';
    }

    toggleBtn.addEventListener('click', () => {
        passwordInput.type = passwordInput.type === 'password' ? 'text' : 'password';
        syncToggleIcon();
    });

    passwordInput.addEventListener('input', () => {
        syncToggleButtonVisibility();
    });

    // Initial state
    syncToggleButtonVisibility();
    syncToggleIcon();
})();

document.getElementById('loginForm')?.addEventListener('submit', async (e) => {
    e.preventDefault();

    const username = document.getElementById('txtUsername').value;
    const password = document.getElementById('txtPassword').value;

    function setCookie(name, value) {
        // Shared across ports on localhost because cookies are host-based, not port-based.
        document.cookie = name + '=' + encodeURIComponent(value || '') + '; path=/; SameSite=Lax';
    }

    try {
        if (typeof window.STREETFOOD_API !== 'string' || !window.STREETFOOD_API) {
            alert('Thiếu cấu hình STREETFOOD_API. Hãy kiểm tra config.js.');
            return;
        }

        const response = await fetch(window.STREETFOOD_API + '/api/Auth/login', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({
                Username: username,
                Password: password
            })
        });

        if (response.ok) {
            const data = await response.json().catch(() => null);
            const role = data?.role;

            if (!role) {
                alert('Login OK nhưng không xác định được role.');
                return;
            }

            localStorage.setItem('userRole', role);

            if (role === 'vendor') {
                setCookie('vendorUsername', username);
                setCookie('vendorPassword', password);
                window.location.href = window.STREETFOOD_VENDOR_DASHBOARD_URL || '/html/dashboardShopPage.html';
                return;
            }

            if (role === 'admin') {
                window.location.href = window.STREETFOOD_ADMIN_DASHBOARD_URL || '/html/dashboardPage.html';
                return;
            }

            alert('Role không hợp lệ: ' + role);
        } else {
            const errorText = await response.text();
            alert('Đăng nhập thất bại: ' + errorText);
        }
    } catch (error) {
        console.error('Lỗi kết nối API:', error);
        alert('Không thể kết nối đến máy chủ API. Hãy chắc chắn StreetFoodAPI đang chạy!');
    }
});

