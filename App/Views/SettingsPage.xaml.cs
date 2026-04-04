using Microsoft.Maui.Storage;
using System.Collections.Generic;
using System.Globalization;

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

        _isInitializing = false;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _isInitializing = true;
        AutoAudioSwitch.IsToggled = Preferences.Default.Get(AutoAudioKey, true);
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
}
