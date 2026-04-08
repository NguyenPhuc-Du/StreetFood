using Microsoft.Maui.Storage;
using System.Collections.Generic;
using System.Globalization;
using App.Services;

namespace App.Views;

public partial class SettingsPage : ContentPage
{
    const string AutoAudioKey = "autoAudioEnabled";
    const string LanguageKey = "appLanguage";
    readonly Dictionary<string, RadioButton> _languageButtons = new();
    readonly HashSet<string> _supportedLanguageCodes = new()
    {
        "vi", "en", "ja", "ko", "zh"
    };
    bool _isInitializing;

    public SettingsPage()
    {
        InitializeComponent();
        _isInitializing = true;

        AutoAudioSwitch.IsToggled = Preferences.Default.Get(AutoAudioKey, true);
        _languageButtons["vi"] = LangViRadio;
        _languageButtons["en"] = LangEnRadio;
        _languageButtons["ja"] = LangJaRadio;
        _languageButtons["ko"] = LangKoRadio;
        _languageButtons["zh"] = LangZhRadio;

        var savedLang = Preferences.Default.Get(LanguageKey, CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
        if (!_supportedLanguageCodes.Contains(savedLang))
            savedLang = "vi";

        if (_languageButtons.TryGetValue(savedLang, out var selectedButton))
            selectedButton.IsChecked = true;
        ApiBaseUrlEntry.Text = ApiConfig.GetBaseUrl();

        _isInitializing = false;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _isInitializing = true;
        AutoAudioSwitch.IsToggled = Preferences.Default.Get(AutoAudioKey, true);
        ApiBaseUrlEntry.Text = ApiConfig.GetBaseUrl();
        _isInitializing = false;
    }

    private void OnAutoAudioSwitchToggled(object? sender, ToggledEventArgs e)
    {
        Preferences.Default.Set(AutoAudioKey, e.Value);
    }

    private async void OnLanguageRadioCheckedChanged(object? sender, CheckedChangedEventArgs e)
    {
        if (_isInitializing || !e.Value || sender is not RadioButton radio || radio.Value is not string selectedCode)
            return;

        Preferences.Default.Set(LanguageKey, selectedCode);
        var culture = new CultureInfo(selectedCode);
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;

        await DisplayAlertAsync("Đã lưu", "Ngôn ngữ đã được cập nhật cho dữ liệu hiển thị mới.", "OK");
    }

    private async void OnSaveApiUrlClicked(object? sender, EventArgs e)
    {
        var raw = ApiBaseUrlEntry.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            await DisplayAlertAsync("Thiếu URL", "Nhập API URL trước khi lưu.", "OK");
            return;
        }
        if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            await DisplayAlertAsync("URL không hợp lệ", "URL phải bắt đầu bằng http:// hoặc https://", "OK");
            return;
        }

        ApiConfig.SetBaseUrl(raw);
        await DisplayAlertAsync("Đã lưu", $"API URL: {ApiConfig.GetBaseUrl()}", "OK");
    }

    private async void OnResetApiUrlClicked(object? sender, EventArgs e)
    {
        ApiConfig.SetBaseUrl(null);
        ApiBaseUrlEntry.Text = ApiConfig.GetBaseUrl();
        await DisplayAlertAsync("Đã đặt mặc định", $"API URL: {ApiConfig.GetBaseUrl()}", "OK");
    }
}
