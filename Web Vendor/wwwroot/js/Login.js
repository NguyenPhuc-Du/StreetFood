document.getElementById('loginForm')?.addEventListener('submit', async (e) => {
    e.preventDefault(); // Ngăn trang web load lại

    const username = document.getElementById('txtUsername').value;
    const password = document.getElementById('txtPassword').value;

    console.log("Đang thử đăng nhập với:", username);

    try {
        // Lưu ý: Đảm bảo cổng 7236 là của dự án StreetFood.API
        const response = await fetch('https://localhost:7236/api/Auth/Login', {
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
            const data = await response.json();

            // Lưu role vào máy để các trang sau kiểm tra
            localStorage.setItem('userRole', data.role);

            // Điều hướng dựa trên role từ Database Neon
            if (data.role === 'admin') {
                // Nhảy ra khỏi thư mục hiện tại và vào Web Admin
                window.location.href = 'https://localhost:7238/html/dashboardPage.html';
            } else if (data.role === 'vendor') {
                // Nhảy ra khỏi thư mục hiện tại và vào Web Vendor
                window.location.href = 'https://localhost:7240/html/dashboardShopPage.html';
            } else {
                alert("Quyền truy cập không hợp lệ: " + data.role);
            }
        }
    } catch (error) {
        console.error("Lỗi kết nối API:", error);
        alert("Không thể kết nối đến máy chủ API. Hãy chắc chắn API đang chạy!");
    }
});