using System;
using System.Threading.Tasks;
using App.Services;

namespace App.Views
{
    public partial class SplashPage : ContentPage
    {
        public SplashPage()
        {
            InitializeComponent();
            ApplyLocalizedTexts();
            StartLoading();
        }

        async void StartLoading()
        {
            for (int i = 0; i <= 100; i++)
            {
                await LoadingBar.ProgressTo(i / 100.0, 30, Easing.Linear);
                PercentLabel.Text = $"{i}%";
            }

            await Task.Delay(300);

            // Không gán Window.Page = AppShell (crash Android). Splash là modal trên Shell.
            await Navigation.PopModalAsync();
        }

        void ApplyLocalizedTexts()
        {
            LoadingLabel.Text = LocalizationService.T("SplashLoading");
        }
    }
}