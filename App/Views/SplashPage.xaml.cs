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

            // SỬA LỖI CS0618: Sử dụng cách chuyển trang mới trong .NET 10
            if (Application.Current?.Windows.Count > 0)
            {
                Application.Current.Windows[0].Page = new AppShell();
            }
        }
    }
}