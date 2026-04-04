using System.Threading;
using App.Services;
using App.Views;
namespace App;

public partial class AppShell : Shell
{
    static int _modalGate;

    public AppShell()
    {
        InitializeComponent();

        Routing.RegisterRoute("poidetail", typeof(PoiDetailPage));

        Navigated += OnShellNavigated;
    }

    async void OnShellNavigated(object? sender, ShellNavigatedEventArgs e)
    {
        try
        {
            await TrySyncActivationFromServerAsync();

            if (ActivationService.IsCurrentlyActivated())
                return;

            if (Navigation.ModalStack.Any(p => p is QrGatePage))
                return;

            if (Interlocked.CompareExchange(ref _modalGate, 1, 0) != 0)
                return;
            try
            {
                await Navigation.PushModalAsync(new QrGatePage());
            }
            finally
            {
                Interlocked.Exchange(ref _modalGate, 0);
            }
        }
        catch
        {
            Interlocked.Exchange(ref _modalGate, 0);
        }
    }

    static async Task TrySyncActivationFromServerAsync()
    {
        if (ActivationService.IsCurrentlyActivated())
            return;
        if (!NetworkReachability.HasUsableConnection)
            return;

        try
        {
            var auth = AuthApiService.Instance;
            var id = ActivationService.GetOrCreateInstallId();
            var (ok, data) = await auth.GetDeviceStatusAsync(id);
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
            // bỏ qua khi API không tới được
        }
    }
}
