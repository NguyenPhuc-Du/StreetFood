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
            console.log("Đăng nhập thành công:", data);

            // Lưu role vào localStorage
            localStorage.setItem('userRole', data.role);

            // Chuyển sang trang Dashboard
            window.location.href = '/html/dashboardShopPage.html';
        } else {
            const errorText = await response.text();
            alert("Đăng nhập thất bại: " + errorText);
        }
    } catch (error) {
        console.error("Lỗi kết nối API:", error);
        alert("Không thể kết nối đến máy chủ API. Hãy chắc chắn API đang chạy!");
    }
});