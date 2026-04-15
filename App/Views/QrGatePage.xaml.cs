using ZXing.Net.Maui;
using ZXing.Net.Maui.Controls;
using App.Services;

namespace App.Views;

public partial class QrGatePage : ContentPage
{
    bool _completed;
    string? _lastBarcodeValue;
    DateTime _lastBarcodeUtc;

    public QrGatePage()
    {
        InitializeComponent();
        ApplyLocalizedTexts();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        LocalizationService.LanguageChanged += OnLanguageChanged;
        ApplyLocalizedTexts();

        if (ActivationService.IsCurrentlyActivated())
        {
            await DismissAsync();
            return;
        }

        if (BarcodeScanning.IsSupported)
        {
            var cam = await Permissions.RequestAsync<Permissions.Camera>();
            if (cam != PermissionStatus.Granted)
            {
                ShowError(LocalizationService.T("QrNeedCameraPermission"));
                ManualPanel.IsVisible = true;
                return;
            }

            CameraFrame.IsVisible = true;
            CameraView.Options = new BarcodeReaderOptions
            {
                Formats = BarcodeFormats.TwoDimensional,
                AutoRotate = true,
                Multiple = false
            };
        }
        else
            ManualPanel.IsVisible = true;
    }

    protected override bool OnBackButtonPressed()
    {
        Dispatcher.Dispatch(async () =>
            await DisplayAlertAsync(
                LocalizationService.T("QrNeedActivationTitle"),
                LocalizationService.T("QrNeedActivationMessage"),
                LocalizationService.T("Ok")));
        return true;
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

    void OnBarcodesDetected(object? sender, BarcodeDetectionEventArgs e)
    {
        if (_completed)
            return;
        var value = e.Results?.FirstOrDefault()?.Value;
        if (string.IsNullOrWhiteSpace(value))
            return;

        var normalized = QrAccess.NormalizePayload(value);
        var now = DateTime.UtcNow;
        if (string.Equals(normalized, _lastBarcodeValue, StringComparison.Ordinal)
            && (now - _lastBarcodeUtc).TotalMilliseconds < 1400)
            return;
        _lastBarcodeValue = normalized;
        _lastBarcodeUtc = now;

        MainThread.BeginInvokeOnMainThread(() => _ = TryAcceptAsync(value));
    }

    void OnManualConfirmClicked(object? sender, EventArgs e)
    {
        _ = TryAcceptAsync(ManualCodeEntry.Text);
    }

    async Task TryAcceptAsync(string? payload)
    {
        if (_completed)
            return;

        ErrorLabel.IsVisible = false;

        payload = QrAccess.NormalizePayload(payload);
        if (!QrAccess.TryParseActivation(payload, out var plan, out var err))
        {
            ShowError(err ?? LocalizationService.T("InvalidCode"));
            return;
        }

        _ = ActivationService.GetOrCreateInstallId();
        _completed = true;
        ActivationService.ApplyLocalFromQr(payload, plan);
        await DismissAsync();
    }

    void ShowError(string message)
    {
        ErrorLabel.Text = message;
        ErrorLabel.IsVisible = true;
    }

    async Task DismissAsync()
    {
        try
        {
            await Navigation.PopModalAsync();
        }
        catch
        {
            _completed = false;
        }
    }

    void ApplyLocalizedTexts()
    {
        QrTitleLabel.Text = LocalizationService.T("QrTitle");
        QrManualDescLabel.Text = LocalizationService.T("QrManualDesc");
        ManualCodeEntry.Placeholder = LocalizationService.T("QrManualPlaceholder");
        QrConfirmButton.Text = LocalizationService.T("QrConfirm");
    }
}
