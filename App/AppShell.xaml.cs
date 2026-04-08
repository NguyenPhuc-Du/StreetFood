using System.Threading;
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
}
