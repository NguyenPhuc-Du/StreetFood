# Appium E2E (Android / iOS)

Mục tiêu: mỗi lần chạy bạn **chọn** Android hoặc iOS (`npm run android` / `npm run ios`). Appium tạo session với `platformName` tương ứng; script in log rõ **`Android`** hay **`iOS`** — đây là thiết bị / simulator **thật** mà session đang điều khiển, không phải quy tắc chẵn/lẻ như k6.

## Yêu cầu

- **Node.js** LTS (v18+)
- **Appium 2** (cài qua npm trong thư mục này) + driver:
  - Android (mọi OS dev thường dùng):

    ```bash
    cd tests/e2e-appium
    npm install
    npx appium driver install uiautomator2
    ```

  - iOS (**chỉ trên macOS** + Xcode + Simulator hoặc máy thật):

    ```bash
    npx appium driver install xcuitest
    ```

- **Android:** SDK Platform Tools (`adb`), máy ảo hoặc máy thật bật USB debugging.
- **iOS:** chỉ chạy `npm run ios` trên Mac; cần file `.app` build cho simulator (hoặc chỉnh `IOS_APP`).

## Build app MAUI

- **Android APK** (ví dụ Debug):

  ```bash
  dotnet build App/App.csproj -f net10.0-android -c Debug
  ```

  Đường APK có thể khác tên file — tìm trong `App/bin/Debug/net10.0-android/*.apk` rồi gán:

  ```bash
  set ANDROID_APP=C:\...\com.companyname.StreetFood-Signed.apk
  ```

- **iOS** (trên Mac, simulator):

  ```bash
  dotnet build App/App.csproj -f net10.0-ios -c Debug
  ```

  Tìm thư mục `App.app` dưới `App/bin/Debug/net10.0-ios/...` và:

  ```bash
  export IOS_APP=/path/to/App.app
  ```

## Chạy test (log platform)

```bash
cd tests/e2e-appium
npm install
npx appium driver install uiautomator2
```

**Android:**

```bash
npm run android
```

**iOS (trên Mac):**

```bash
npm run ios
```

Trong console sẽ có các dòng kiểu:

- `[E2E] PLATFORM=android  →  session: Android`
- `[E2E][test] Thiết bị / session: Android (platformName=Android)`

và tương tự cho **iOS** khi chạy `npm run ios`.

## Biến môi trường tùy chọn

| Biến | Ý nghĩa |
|------|--------|
| `ANDROID_APP` | Đường đầy đủ tới file `.apk` |
| `IOS_APP` | Đường đầy đủ tới bundle `.app` (simulator) |
| `IOS_DEVICE_NAME` | Tên simulator (mặc định `iPhone 16`) |
| `IOS_PLATFORM_VERSION` | Phiên bản iOS simulator (mặc định `18.0`) |

## Lưu ý

- **Windows:** chỉ Android E2E thực tế; iOS cần Mac + Xcode.
- Nếu `appium driver install` báo thiếu dependency, làm theo thông báo Appium (JDK, `ANDROID_HOME`, v.v.).
- Test mẫu chỉ kiểm tra session + log platform; bạn có thể thêm bước bấm nút sau khi gắn `~accessibility id` / `AutomationId` trong XAML.
