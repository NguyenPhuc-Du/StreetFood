using System;
using System.Threading.Tasks;

namespace App.Views
{
    public partial class SplashPage : ContentPage
    {
        public SplashPage()
        {
            InitializeComponent();
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

            // Chuyển sang AppShell để hiển thị Bottom Navigation
            Application.Current!.MainPage = new AppShell();
        }
    }
}