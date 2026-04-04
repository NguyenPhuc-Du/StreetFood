using App.Services;
using ZXing.Net.Maui;
using ZXing.Net.Maui.Controls;

namespace App.Views;

public partial class QrGatePage : ContentPage
{
    bool _completed;
    readonly AuthApiService _auth = AuthApiService.Instance;
    string? _lastBarcodeValue;
    DateTime _lastBarcodeUtc;

    public QrGatePage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (ActivationService.IsCurrentlyActivated())
        {
            await DismissAsync();
            return;
        }

        _ = PullServerThenMaybeDismissAsync();

        if (BarcodeScanning.IsSupported)
        {
            var cam = await Permissions.RequestAsync<Permissions.Camera>();
            if (cam != PermissionStatus.Granted)
            {
                ShowError("Cần quyền camera để quét mã QR. Bạn có thể nhập mã thủ công bên dưới.");
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

    async Task PullServerThenMaybeDismissAsync()
    {
        await TryPullServerStatusAsync();
        if (_completed)
            return;
        if (ActivationService.IsCurrentlyActivated())
            await MainThread.InvokeOnMainThreadAsync(DismissAsync);
    }

    async Task TryPullServerStatusAsync()
    {
        if (!NetworkReachability.HasUsableConnection)
            return;
        try
        {
            var id = ActivationService.GetOrCreateInstallId();
            var (ok, data) = await _auth.GetDeviceStatusAsync(id);
            if (!ok || data == null || !data.Active || string.IsNullOrEmpty(data.ActivationExpiresAt))
                return;
            if (!DateTime.TryParse(data.ActivationExpiresAt, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind, out var exp))
                return;
            if (exp <= DateTime.UtcNow)
                return;
            ActivationService.ApplyServerUtc(exp, data.PlanLabel);
        }
        catch
        {
            // ignore
        }
    }

    protected override bool OnBackButtonPressed()
    {
        Dispatcher.Dispatch(async () =>
            await DisplayAlertAsync("Cần kích hoạt", "Quét mã hợp lệ. Có internet và API dùng được thì đồng bộ máy chủ; không thì app vẫn kích hoạt cục bộ.", "OK"));
        return true;
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
            ShowError(err ?? "Mã không hợp lệ.");
            return;
        }

        if (NetworkReachability.HasUsableConnection)
        {
            BusyIndicator.IsVisible = true;
            BusyIndicator.IsRunning = true;
            try
            {
                var installId = ActivationService.GetOrCreateInstallId();
                var (ok, error, data, transientNetworkFailure) = await _auth.ActivateDeviceAsync(installId, payload);
                if (ok && data?.ActivationExpiresAt != null
                    && DateTime.TryParse(data.ActivationExpiresAt, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind, out var exp))
                {
                    _completed = true;
                    ActivationService.ApplyServerUtc(exp, data.PlanLabel);
                    await DismissAsync();
                    return;
                }

                if (!transientNetworkFailure)
                {
                    ShowError(string.IsNullOrWhiteSpace(error) ? "Không kích hoạt được." : error);
                    return;
                }
            }
            finally
            {
                BusyIndicator.IsRunning = false;
                BusyIndicator.IsVisible = false;
            }
        }

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
}
