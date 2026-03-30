using CommunityToolkit.Maui;
#if ANDROID
using Android.Gms.Maps;
#endif

namespace App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiMaps()
            .UseMauiCommunityToolkitMediaElement(true)
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

#if ANDROID
        Microsoft.Maui.Maps.Handlers.MapHandler.Mapper.AppendToMapping("HideGoogleMapControls", (handler, view) =>
        {
            handler.PlatformView.GetMapAsync(new NoControlsMapReadyCallback());
        });
#endif

        return builder.Build();
    }
}

#if ANDROID
internal sealed class NoControlsMapReadyCallback : Java.Lang.Object, IOnMapReadyCallback
{
    public void OnMapReady(GoogleMap googleMap)
    {
        googleMap.UiSettings.ZoomControlsEnabled = false;
        googleMap.UiSettings.MyLocationButtonEnabled = false;
        googleMap.UiSettings.MapToolbarEnabled = false;
    }
}
#endif