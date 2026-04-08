using App.Models;
using App.Services;
using Microsoft.Maui.Maps;
using System.Collections.ObjectModel;

namespace App.Views;

public partial class SuggestPage : ContentPage
{
    private readonly ApiService _api = ApiService.Instance;

    public ObservableCollection<SuggestionItem> Suggestions { get; } = new();

    public SuggestPage()
    {
        InitializeComponent();
        BindingContext = this;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadSuggestions();
    }

    private async Task LoadSuggestions()
    {
        Suggestions.Clear();
        LoadingIndicator.IsVisible = true;
        LoadingIndicator.IsRunning = true;
        SuggestionsList.IsVisible = false;

        var status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
        if (status != PermissionStatus.Granted)
        {
            LoadingIndicator.IsRunning = false;
            LoadingIndicator.IsVisible = false;
            SubTitleLabel.Text = "Chưa cấp quyền vị trí.";
            return;
        }

        try
        {
            var geoReq = new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(8));
            var locTask = Geolocation.Default.GetLocationAsync(geoReq);
            var poisTask = _api.GetTopPois(10, 30);

            List<Poi> pois = new();
            Location? loc = null;
            try
            {
                await Task.WhenAll(locTask, poisTask);
                pois = await poisTask;
                loc = await locTask;
            }
            catch
            {
                try
                {
                    if (poisTask.IsCompletedSuccessfully)
                        pois = poisTask.Result;
                    else
                        pois = await poisTask;
                }
                catch
                {
                    pois = new List<Poi>();
                }

                try
                {
                    if (locTask.IsCompletedSuccessfully)
                        loc = locTask.Result;
                    else if (!locTask.IsFaulted)
                        loc = await locTask;
                }
                catch
                {
                    loc = null;
                }
            }

            if (loc == null)
            {
                SubTitleLabel.Text = "Không lấy được vị trí.";
                return;
            }

            if (pois.Count == 0)
            {
                SubTitleLabel.Text = "Không có dữ liệu quán ăn.";
                return;
            }

            var nearest = pois
                .Select(p => new SuggestionItem
                {
                    Poi = p,
                    DistanceMeters = loc.CalculateDistance(new Location(p.Latitude, p.Longitude), DistanceUnits.Kilometers) * 1000
                })
                .OrderByDescending(x => x.Poi.VisitCount)
                .ThenBy(x => x.DistanceMeters)
                .Take(10)
                .ToList();

            foreach (var item in nearest)
            {
                item.DistanceText = $"{item.DistanceMeters:0} m";
                Suggestions.Add(item);
            }

            SubTitleLabel.Text = "Chạm vào quán để mở chi tiết.";
            SuggestionsList.IsVisible = true;
        }
        catch
        {
            SubTitleLabel.Text = "Lỗi khi tải gợi ý.";
        }
        finally
        {
            LoadingIndicator.IsRunning = false;
            LoadingIndicator.IsVisible = false;
        }
    }

    private async void OnSuggestionTapped(object? sender, TappedEventArgs e)
    {
        var selected = e.Parameter as SuggestionItem;
        if (selected?.Poi?.Id != null)
            await Shell.Current.GoToAsync($"poidetail?poiId={selected.Poi.Id}");
    }

    public class SuggestionItem
    {
        public Poi Poi { get; set; } = default!;
        public double DistanceMeters { get; set; }
        public string DistanceText { get; set; } = "";
    }
}
