using System.Globalization;
using Microsoft.Maui.Storage;

namespace App.Services;

public static class LocalizationService
{
    public const string LanguageKey = "appLanguage";

    static readonly HashSet<string> SupportedLanguages = new(StringComparer.OrdinalIgnoreCase)
    {
        "vi", "en", "ja", "ko", "zh"
    };

    static readonly Dictionary<string, Dictionary<string, string>> Texts = new(StringComparer.OrdinalIgnoreCase)
    {
        ["vi"] = new(StringComparer.Ordinal)
        {
            ["TabMap"] = "Bản đồ",
            ["TabSuggest"] = "Đề xuất",
            ["TabSettings"] = "Cài đặt",
            ["HomeSearchPlaceholder"] = "Tìm quán ăn hoặc POI",
            ["HomeMyLocation"] = "Vị trí của tôi",
            ["HomeDragToClose"] = "Kéo xuống để đóng thẻ và tắt âm thanh",
            ["Close"] = "Đóng",
            ["Play"] = "Phát",
            ["Pause"] = "Tạm dừng",
            ["Address"] = "Địa chỉ",
            ["Phone"] = "SĐT",
            ["Open"] = "OPEN",
            ["UnableLoadShopsTitle"] = "Chưa tải được dữ liệu quán",
            ["UnableLoadShopsMessage"] = "Không có POI từ API.\nKiểm tra API URL trong Cài đặt:\n{0}",
            ["ApiConnectionErrorTitle"] = "Lỗi kết nối API",
            ["ApiConnectionErrorMessage"] = "Không thể gọi API tại:\n{0}\nHãy mở Cài đặt để sửa API Base URL.",
            ["PoiHeaderDetails"] = "Chi tiết",
            ["PoiFoods"] = "Các món ăn",
            ["PoiAudio"] = "Audio cửa hàng",
            ["PoiNavigate"] = "Đi đến quán",
            ["PoiPhone"] = "Số điện thoại",
            ["PoiOpenHours"] = "Giờ mở cửa",
            ["SuggestBadge"] = "Gợi ý theo vị trí",
            ["SuggestTitle"] = "Đề xuất gần bạn",
            ["SuggestLoading"] = "Đang lấy vị trí...",
            ["SuggestNoPermission"] = "Chưa cấp quyền vị trí.",
            ["SuggestNoLocation"] = "Không lấy được vị trí.",
            ["SuggestNoData"] = "Không có dữ liệu quán ăn.",
            ["SuggestTapHint"] = "Chạm vào quán để mở chi tiết.",
            ["SuggestLoadError"] = "Lỗi khi tải gợi ý.",
            ["SettingsTitle"] = "Cài đặt",
            ["SettingsSubtitle"] = "Tùy chỉnh trải nghiệm phù hợp với bạn",
            ["SettingsAutoAudioTitle"] = "Tự động phát audio khi ở gần",
            ["SettingsAutoAudioDesc"] = "Tự phát audio khi bạn đi vào phạm vi của quán trên bản đồ.",
            ["SettingsLanguageTitle"] = "Ngôn ngữ ứng dụng",
            ["SettingsLanguageDesc"] = "Chọn ngôn ngữ bạn muốn dùng để nhận nội dung phù hợp.",
            ["SettingsSuggestTitle"] = "Gợi ý trải nghiệm",
            ["SettingsSuggestDesc"] = "Vào trang gợi ý để xem quán gần nhất và mở nhanh trang chi tiết.",
            ["Saved"] = "Đã lưu",
            ["LanguageUpdated"] = "Ngôn ngữ đã được cập nhật cho dữ liệu hiển thị mới.",
            ["QrTitle"] = "Quét mã QR",
            ["QrManualDesc"] = "Thiết bị không hỗ trợ camera quét (ví dụ Windows). Nhập nội dung mã thủ công.",
            ["QrManualPlaceholder"] = "Nhập mã / nội dung QR",
            ["QrConfirm"] = "Xác nhận",
            ["QrNeedCameraPermission"] = "Cần quyền camera để quét mã QR. Bạn có thể nhập mã thủ công bên dưới.",
            ["QrNeedActivationTitle"] = "Cần kích hoạt",
            ["QrNeedActivationMessage"] = "Quét JWT QR hợp lệ để dùng app. Kích hoạt lưu cục bộ 7 ngày.",
            ["InvalidCode"] = "Mã không hợp lệ.",
            ["Ok"] = "OK",
            ["SplashLoading"] = "Đang tải"
        },
        ["en"] = new(StringComparer.Ordinal)
        {
            ["TabMap"] = "Map",
            ["TabSuggest"] = "Suggestions",
            ["TabSettings"] = "Settings",
            ["HomeSearchPlaceholder"] = "Search restaurants or POIs",
            ["HomeMyLocation"] = "My Location",
            ["HomeDragToClose"] = "Swipe down to close the card and stop audio",
            ["Close"] = "Close",
            ["Play"] = "Play",
            ["Pause"] = "Pause",
            ["Address"] = "Address",
            ["Phone"] = "Phone",
            ["Open"] = "OPEN",
            ["UnableLoadShopsTitle"] = "Could not load places",
            ["UnableLoadShopsMessage"] = "No POI returned from API.\nCheck API URL in Settings:\n{0}",
            ["ApiConnectionErrorTitle"] = "API connection error",
            ["ApiConnectionErrorMessage"] = "Cannot reach API at:\n{0}\nPlease update API Base URL in Settings.",
            ["PoiHeaderDetails"] = "Details",
            ["PoiFoods"] = "Menu",
            ["PoiAudio"] = "Store Audio",
            ["PoiNavigate"] = "Navigate",
            ["PoiPhone"] = "Phone",
            ["PoiOpenHours"] = "Opening hours",
            ["SuggestBadge"] = "Location-based",
            ["SuggestTitle"] = "Nearby Suggestions",
            ["SuggestLoading"] = "Getting your location...",
            ["SuggestNoPermission"] = "Location permission not granted.",
            ["SuggestNoLocation"] = "Unable to get location.",
            ["SuggestNoData"] = "No restaurant data available.",
            ["SuggestTapHint"] = "Tap a place to open details.",
            ["SuggestLoadError"] = "Failed to load suggestions.",
            ["SettingsTitle"] = "Settings",
            ["SettingsSubtitle"] = "Customize the app experience for you",
            ["SettingsAutoAudioTitle"] = "Auto-play audio nearby",
            ["SettingsAutoAudioDesc"] = "Automatically play audio when entering a place radius on the map.",
            ["SettingsLanguageTitle"] = "App language",
            ["SettingsLanguageDesc"] = "Choose your preferred language for app content.",
            ["SettingsSuggestTitle"] = "Experience hints",
            ["SettingsSuggestDesc"] = "Use Suggestions to quickly open nearby place details.",
            ["Saved"] = "Saved",
            ["LanguageUpdated"] = "Language has been updated for newly displayed content.",
            ["QrTitle"] = "Scan QR Code",
            ["QrManualDesc"] = "This device cannot scan QR codes (for example Windows). Enter code manually.",
            ["QrManualPlaceholder"] = "Enter code / QR payload",
            ["QrConfirm"] = "Confirm",
            ["QrNeedCameraPermission"] = "Camera permission is required to scan QR. You can enter code manually below.",
            ["QrNeedActivationTitle"] = "Activation required",
            ["QrNeedActivationMessage"] = "Scan a valid JWT QR to use the app. Activation is stored locally for 7 days.",
            ["InvalidCode"] = "Invalid code.",
            ["Ok"] = "OK",
            ["SplashLoading"] = "Loading"
        },
        ["ja"] = new(StringComparer.Ordinal)
        {
            ["TabMap"] = "地図",
            ["TabSuggest"] = "おすすめ",
            ["TabSettings"] = "設定",
            ["HomeSearchPlaceholder"] = "レストランまたはPOIを検索",
            ["HomeMyLocation"] = "現在地",
            ["HomeDragToClose"] = "下にスワイプしてカードを閉じ、音声を停止",
            ["Close"] = "閉じる",
            ["Play"] = "再生",
            ["Pause"] = "一時停止",
            ["Address"] = "住所",
            ["Phone"] = "電話",
            ["Open"] = "営業中",
            ["UnableLoadShopsTitle"] = "スポットを読み込めません",
            ["UnableLoadShopsMessage"] = "APIからPOIデータが取得できませんでした。\n設定でAPI URLを確認してください:\n{0}",
            ["ApiConnectionErrorTitle"] = "API接続エラー",
            ["ApiConnectionErrorMessage"] = "APIに接続できません:\n{0}\n設定でAPI Base URLを修正してください。",
            ["PoiHeaderDetails"] = "詳細",
            ["PoiFoods"] = "メニュー",
            ["PoiAudio"] = "店舗音声",
            ["PoiNavigate"] = "店舗へ移動",
            ["PoiPhone"] = "電話番号",
            ["PoiOpenHours"] = "営業時間",
            ["SuggestBadge"] = "位置情報ベース",
            ["SuggestTitle"] = "近くのおすすめ",
            ["SuggestLoading"] = "現在地を取得中...",
            ["SuggestNoPermission"] = "位置情報の権限がありません。",
            ["SuggestNoLocation"] = "現在地を取得できません。",
            ["SuggestNoData"] = "店舗データがありません。",
            ["SuggestTapHint"] = "店舗をタップして詳細を開きます。",
            ["SuggestLoadError"] = "おすすめの読み込みに失敗しました。",
            ["SettingsTitle"] = "設定",
            ["SettingsSubtitle"] = "あなたに合わせてアプリ体験をカスタマイズ",
            ["SettingsAutoAudioTitle"] = "近くで音声を自動再生",
            ["SettingsAutoAudioDesc"] = "地図上で店舗の範囲に入ると音声を自動再生します。",
            ["SettingsLanguageTitle"] = "アプリの言語",
            ["SettingsLanguageDesc"] = "表示に使用する言語を選択してください。",
            ["SettingsSuggestTitle"] = "利用のヒント",
            ["SettingsSuggestDesc"] = "おすすめページから近くの店舗詳細を素早く開けます。",
            ["Saved"] = "保存しました",
            ["LanguageUpdated"] = "新しく表示される内容の言語を更新しました。",
            ["QrTitle"] = "QRコードをスキャン",
            ["QrManualDesc"] = "この端末ではQRスキャンに対応していません（例: Windows）。手動で入力してください。",
            ["QrManualPlaceholder"] = "コード / QR内容を入力",
            ["QrConfirm"] = "確認",
            ["QrNeedCameraPermission"] = "QRをスキャンするにはカメラ権限が必要です。下で手動入力もできます。",
            ["QrNeedActivationTitle"] = "有効化が必要です",
            ["QrNeedActivationMessage"] = "有効なJWT QRをスキャンしてアプリを利用してください。7日間ローカル保存されます。",
            ["InvalidCode"] = "無効なコードです。",
            ["Ok"] = "OK",
            ["SplashLoading"] = "読み込み中"
        },
        ["ko"] = new(StringComparer.Ordinal)
        {
            ["TabMap"] = "지도",
            ["TabSuggest"] = "추천",
            ["TabSettings"] = "설정",
            ["HomeSearchPlaceholder"] = "식당 또는 POI 검색",
            ["HomeMyLocation"] = "내 위치",
            ["HomeDragToClose"] = "아래로 스와이프하여 카드를 닫고 오디오를 중지",
            ["Close"] = "닫기",
            ["Play"] = "재생",
            ["Pause"] = "일시정지",
            ["Address"] = "주소",
            ["Phone"] = "전화",
            ["Open"] = "영업중",
            ["UnableLoadShopsTitle"] = "장소를 불러올 수 없습니다",
            ["UnableLoadShopsMessage"] = "API에서 POI 데이터를 받지 못했습니다.\n설정에서 API URL을 확인하세요:\n{0}",
            ["ApiConnectionErrorTitle"] = "API 연결 오류",
            ["ApiConnectionErrorMessage"] = "다음 API에 연결할 수 없습니다:\n{0}\n설정에서 API Base URL을 수정하세요.",
            ["PoiHeaderDetails"] = "상세",
            ["PoiFoods"] = "메뉴",
            ["PoiAudio"] = "매장 오디오",
            ["PoiNavigate"] = "매장으로 이동",
            ["PoiPhone"] = "전화번호",
            ["PoiOpenHours"] = "영업시간",
            ["SuggestBadge"] = "위치 기반",
            ["SuggestTitle"] = "근처 추천",
            ["SuggestLoading"] = "위치 정보를 가져오는 중...",
            ["SuggestNoPermission"] = "위치 권한이 허용되지 않았습니다.",
            ["SuggestNoLocation"] = "위치를 가져올 수 없습니다.",
            ["SuggestNoData"] = "식당 데이터가 없습니다.",
            ["SuggestTapHint"] = "가게를 눌러 상세 페이지를 여세요.",
            ["SuggestLoadError"] = "추천을 불러오는 중 오류가 발생했습니다.",
            ["SettingsTitle"] = "설정",
            ["SettingsSubtitle"] = "나에게 맞게 앱 경험을 설정하세요",
            ["SettingsAutoAudioTitle"] = "근처에서 오디오 자동 재생",
            ["SettingsAutoAudioDesc"] = "지도에서 매장 범위에 들어오면 오디오를 자동 재생합니다.",
            ["SettingsLanguageTitle"] = "앱 언어",
            ["SettingsLanguageDesc"] = "앱 콘텐츠에 사용할 언어를 선택하세요.",
            ["SettingsSuggestTitle"] = "사용 팁",
            ["SettingsSuggestDesc"] = "추천 페이지에서 가까운 매장 상세를 빠르게 열 수 있습니다.",
            ["Saved"] = "저장됨",
            ["LanguageUpdated"] = "새로 표시되는 콘텐츠의 언어가 업데이트되었습니다.",
            ["QrTitle"] = "QR 코드 스캔",
            ["QrManualDesc"] = "이 기기는 QR 스캔을 지원하지 않습니다(예: Windows). 수동으로 입력하세요.",
            ["QrManualPlaceholder"] = "코드 / QR 내용 입력",
            ["QrConfirm"] = "확인",
            ["QrNeedCameraPermission"] = "QR 스캔을 위해 카메라 권한이 필요합니다. 아래에서 수동 입력도 가능합니다.",
            ["QrNeedActivationTitle"] = "활성화 필요",
            ["QrNeedActivationMessage"] = "유효한 JWT QR을 스캔해야 앱을 사용할 수 있습니다. 7일간 로컬에 저장됩니다.",
            ["InvalidCode"] = "유효하지 않은 코드입니다.",
            ["Ok"] = "확인",
            ["SplashLoading"] = "로딩 중"
        },
        ["zh"] = new(StringComparer.Ordinal)
        {
            ["TabMap"] = "地图",
            ["TabSuggest"] = "推荐",
            ["TabSettings"] = "设置",
            ["HomeSearchPlaceholder"] = "搜索餐厅或POI",
            ["HomeMyLocation"] = "我的位置",
            ["HomeDragToClose"] = "下滑关闭卡片并停止音频",
            ["Close"] = "关闭",
            ["Play"] = "播放",
            ["Pause"] = "暂停",
            ["Address"] = "地址",
            ["Phone"] = "电话",
            ["Open"] = "营业中",
            ["UnableLoadShopsTitle"] = "无法加载地点数据",
            ["UnableLoadShopsMessage"] = "API 未返回 POI 数据。\n请在设置中检查 API URL：\n{0}",
            ["ApiConnectionErrorTitle"] = "API 连接错误",
            ["ApiConnectionErrorMessage"] = "无法连接到以下 API：\n{0}\n请在设置中修改 API Base URL。",
            ["PoiHeaderDetails"] = "详情",
            ["PoiFoods"] = "菜品",
            ["PoiAudio"] = "店铺语音",
            ["PoiNavigate"] = "前往店铺",
            ["PoiPhone"] = "联系电话",
            ["PoiOpenHours"] = "营业时间",
            ["SuggestBadge"] = "基于位置",
            ["SuggestTitle"] = "附近推荐",
            ["SuggestLoading"] = "正在获取位置...",
            ["SuggestNoPermission"] = "未授予定位权限。",
            ["SuggestNoLocation"] = "无法获取位置。",
            ["SuggestNoData"] = "暂无餐厅数据。",
            ["SuggestTapHint"] = "点击店铺查看详情。",
            ["SuggestLoadError"] = "加载推荐失败。",
            ["SettingsTitle"] = "设置",
            ["SettingsSubtitle"] = "按你的习惯自定义应用体验",
            ["SettingsAutoAudioTitle"] = "附近自动播放语音",
            ["SettingsAutoAudioDesc"] = "进入地图上店铺范围时自动播放语音。",
            ["SettingsLanguageTitle"] = "应用语言",
            ["SettingsLanguageDesc"] = "选择你希望使用的应用语言。",
            ["SettingsSuggestTitle"] = "使用建议",
            ["SettingsSuggestDesc"] = "在推荐页面快速打开附近店铺详情。",
            ["Saved"] = "已保存",
            ["LanguageUpdated"] = "新显示的内容语言已更新。",
            ["QrTitle"] = "扫描二维码",
            ["QrManualDesc"] = "设备不支持二维码扫描（例如 Windows），请手动输入内容。",
            ["QrManualPlaceholder"] = "输入代码 / 二维码内容",
            ["QrConfirm"] = "确认",
            ["QrNeedCameraPermission"] = "扫描二维码需要相机权限。你也可以在下方手动输入。",
            ["QrNeedActivationTitle"] = "需要激活",
            ["QrNeedActivationMessage"] = "请扫描有效的 JWT 二维码后再使用应用。激活信息将在本地保存 7 天。",
            ["InvalidCode"] = "无效代码。",
            ["Ok"] = "确定",
            ["SplashLoading"] = "加载中"
        }
    };

    static LocalizationService()
    {
        var lang = NormalizeLanguage(Preferences.Default.Get(LanguageKey, CultureInfo.CurrentUICulture.TwoLetterISOLanguageName));
        ApplyCulture(lang);
    }

    public static event EventHandler? LanguageChanged;

    public static string CurrentLanguage => NormalizeLanguage(
        Preferences.Default.Get(LanguageKey, CultureInfo.CurrentUICulture.TwoLetterISOLanguageName));

    public static void SetLanguage(string? languageCode)
    {
        var lang = NormalizeLanguage(languageCode);
        Preferences.Default.Set(LanguageKey, lang);
        ApplyCulture(lang);
        LanguageChanged?.Invoke(null, EventArgs.Empty);
    }

    public static string T(string key)
    {
        var lang = CurrentLanguage;
        if (Texts.TryGetValue(lang, out var map) && map.TryGetValue(key, out var text))
            return text;
        if (Texts.TryGetValue("en", out var en) && en.TryGetValue(key, out var enText))
            return enText;
        if (Texts.TryGetValue("vi", out var vi) && vi.TryGetValue(key, out var viText))
            return viText;
        return key;
    }

    public static string Tf(string key, params object[] args) =>
        string.Format(CultureInfo.CurrentCulture, T(key), args);

    static string NormalizeLanguage(string? languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode)) return "vi";
        var code = languageCode.Trim().ToLowerInvariant();
        if (code == "cn") code = "zh";
        return SupportedLanguages.Contains(code) ? code : "vi";
    }

    static void ApplyCulture(string lang)
    {
        var culture = new CultureInfo(lang);
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
    }
}
