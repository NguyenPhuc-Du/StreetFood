using Microsoft.Maui.Storage;
using System.Collections.Generic;
using System.Globalization;
using App.Services;

namespace App.Views;

public partial class SettingsPage : ContentPage
{
    const string AutoAudioKey = "autoAudioEnabled";
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

        var savedLang = Preferences.Default.Get(LocalizationService.LanguageKey, CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
        if (!_supportedLanguageCodes.Contains(savedLang))
            savedLang = "vi";

        if (_languageButtons.TryGetValue(savedLang, out var selectedButton))
            selectedButton.IsChecked = true;

        ApplyLocalizedTexts();
        _isInitializing = false;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _isInitializing = true;
        AutoAudioSwitch.IsToggled = Preferences.Default.Get(AutoAudioKey, true);
        _isInitializing = false;
        LocalizationService.LanguageChanged += OnLanguageChanged;
        ApplyLocalizedTexts();
    }

    protected override void OnDisappearing()
    {
        LocalizationService.LanguageChanged -= OnLanguageChanged;
        base.OnDisappearing();
    }

    void OnLanguageChanged(object? sender, EventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(ApplyLocalizedTexts);
    }

    private void OnAutoAudioSwitchToggled(object? sender, ToggledEventArgs e)
    {
        Preferences.Default.Set(AutoAudioKey, e.Value);
    }

    private async void OnLanguageRadioCheckedChanged(object? sender, CheckedChangedEventArgs e)
    {
        if (_isInitializing || !e.Value || sender is not RadioButton radio || radio.Value is not string selectedCode)
            return;

        LocalizationService.SetLanguage(selectedCode);

        await DisplayAlertAsync(LocalizationService.T("Saved"), LocalizationService.T("LanguageUpdated"), LocalizationService.T("Ok"));
    }

    void ApplyLocalizedTexts()
    {
        SettingsTitleLabel.Text = LocalizationService.T("SettingsTitle");
        SettingsSubtitleLabel.Text = LocalizationService.T("SettingsSubtitle");
        AutoAudioTitleLabel.Text = LocalizationService.T("SettingsAutoAudioTitle");
        AutoAudioDescLabel.Text = LocalizationService.T("SettingsAutoAudioDesc");
        LanguageTitleLabel.Text = LocalizationService.T("SettingsLanguageTitle");
        LanguageDescLabel.Text = LocalizationService.T("SettingsLanguageDesc");
        SuggestHintTitleLabel.Text = LocalizationService.T("SettingsSuggestTitle");
        SuggestHintDescLabel.Text = LocalizationService.T("SettingsSuggestDesc");
    }
}
