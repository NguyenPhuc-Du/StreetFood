namespace App
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
            _ = ActivationService.GetOrCreateInstallId();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            // Android: đổi Window.Page từ ContentPage sang Shell gây lỗi fragment
            // (NavigationRootManager / item_touch_helper_previous_elevation).
            return new Window(new AppShell());
        }
    }
}