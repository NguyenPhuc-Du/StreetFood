(function () {
    const passwordInput = document.getElementById('txtPassword');
    const toggleBtn = document.getElementById('togglePasswordBtn');
    const eyeOpenIcon = document.getElementById('eyeOpenIcon');
    const eyeClosedIcon = document.getElementById('eyeClosedIcon');

    if (!passwordInput || !toggleBtn) return;

    function syncToggleIcon() {
        const isPasswordHidden = passwordInput.type === 'password';
        // Mật khẩu đang ẩn (dạng •••): icon mắt mở = bấm để hiện. Đang hiện chữ: icon gạch = bấm để ẩn.
        if (eyeOpenIcon && eyeClosedIcon) {
            eyeOpenIcon.classList.toggle('hidden', !isPasswordHidden);
            eyeClosedIcon.classList.toggle('hidden', isPasswordHidden);
        }
        toggleBtn.setAttribute('aria-label', isPasswordHidden ? 'Hiện mật khẩu' : 'Ẩn mật khẩu');
        toggleBtn.setAttribute('title', isPasswordHidden ? 'Hiện mật khẩu' : 'Ẩn mật khẩu');
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

const loginForm = document.getElementById('loginForm');
if (loginForm && loginForm.dataset.boundLoginHandler !== '1') {
    loginForm.dataset.boundLoginHandler = '1';
    let isSubmitting = false;
    let navigating = false;

    loginForm.addEventListener('submit', async (e) => {
        e.preventDefault();
        if (isSubmitting) return;
        isSubmitting = true;

        const username = document.getElementById('txtUsername').value;
        const password = document.getElementById('txtPassword').value;
        const submitBtn = loginForm.querySelector('button[type="submit"]');
        if (submitBtn) submitBtn.disabled = true;

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
                    navigating = true;
                    window.location.href = window.STREETFOOD_VENDOR_DASHBOARD_URL || '/html/dashboardShopPage.html';
                    return;
                }

                if (role === 'admin') {
                    navigating = true;
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
            // Tránh alert giả khi đang chuyển trang do login thành công từ request trước.
            if (!navigating) {
                alert('Không thể kết nối đến máy chủ API. Hãy chắc chắn StreetFoodAPI đang chạy!');
            }
        } finally {
            if (!navigating) {
                isSubmitting = false;
                if (submitBtn) submitBtn.disabled = false;
            }
        }
    });
}

