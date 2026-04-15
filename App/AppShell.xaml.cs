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

    void ApplyLocalizedTexts()
    {
        if (Items.Count == 0 || Items[0] is not TabBar tabBar || tabBar.Items.Count < 3)
            return;
        tabBar.Items[0].Title = LocalizationService.T("TabMap");
        tabBar.Items[1].Title = LocalizationService.T("TabSuggest");
        tabBar.Items[2].Title = LocalizationService.T("TabSettings");
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
