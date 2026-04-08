using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.Maps;
using NBH.Resources.Models;
using NBH.Resources.ViewModels;
using NBH.Services;
#if IOS
using NBH.Platforms.iOS;
#endif

namespace NBH
{
    public partial class MapPage : ContentPage
    {
        private readonly MapPageViewModel _viewModel;
        private readonly ILogger<MapPage> _logger;
        private CancellationTokenSource? _viewportReloadDebounceCts;

        public MapPage(MapPageViewModel viewModel, ILogger<MapPage> logger)
        {
            InitializeComponent();
            _viewModel = viewModel;
            _logger = logger;
            BindingContext = _viewModel;

            _viewModel.Pins.CollectionChanged += (_, _) => RenderPins();
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            MainMap.PropertyChanged += OnMapPropertyChanged;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            try
            {
                MyTabBar.SetActiveTabWithoutNavigation(1);  // 1 = Karte
                await MyBell.UpdateBadgeAsync();

                await _viewModel.InitializeAsync();
                await CenterMapOnCurrentLocationAsync(showWarningIfDenied: false);
                await ReloadForCurrentViewportAsync(force: true);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MapPage] OnAppearing Fehler: {ex.Message}");
            }
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            _viewportReloadDebounceCts?.Cancel();
        }

        private async Task ReloadForCurrentViewportAsync(bool force = false)
        {
            await _viewModel.ReloadForViewportAsync(MainMap.VisibleRegion, force);
        }

        private void RenderPins()
        {
            MainMap.Pins.Clear();
            foreach (var item in _viewModel.Pins)
            {
                var pin = new Pin
                {
                    Label = string.IsNullOrWhiteSpace(item.Title) ? "Event" : item.Title.Trim(),
                    Address = item.Location ?? string.Empty,
                    Location = new Location(item.Latitude, item.Longitude),
                    Type = PinType.Place
                };

                pin.MarkerClicked += async (s, e) =>
                {
                    e.HideInfoWindow = true;
                    await Shell.Current.GoToAsync($"{nameof(DetailPage)}?EntryId={item.Id}");
                };

                MainMap.Pins.Add(pin);
            }
        }

        private async Task CenterMapOnCurrentLocationAsync(bool showWarningIfDenied)
        {
            try
            {
                var location = await _viewModel.TryGetCurrentLocationAsync();
                if (location == null)
                {
                    if (showWarningIfDenied)
                    {
                        await ToastService.ShowWarningAsync(
                            "Standortberechtigung benötigt. Bitte aktiviere den Standort in den Einstellungen.");
                    }
                    return;
                }

                MainMap.MoveToRegion(MapSpan.FromCenterAndRadius(
                    new Location(location.Latitude, location.Longitude),
                    Distance.FromKilometers(5)));

                await ReloadForCurrentViewportAsync(force: true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Zentrieren auf aktuellen Standort.");
            }
        }

        private async void OnLoadCurrentLocationClicked(object sender, EventArgs e)
        {
            await CenterMapOnCurrentLocationAsync(showWarningIfDenied: true);
        }

        private async void OnSearchClicked(object sender, EventArgs e)
        {
            var searchQuery = SearchEntry.Text?.Trim();
            if (string.IsNullOrWhiteSpace(searchQuery))
            {
                await ToastService.ShowWarningAsync("Bitte gib einen gültigen Ort ein.");
                return;
            }

            var location = await _viewModel.SearchLocationAsync(searchQuery);
            if (location == null)
            {
#if IOS
                iOSHapticFeedback.TriggerHeavyImpact();
#endif
                HapticFeedback.Perform(HapticFeedbackType.Click);
                await ToastService.ShowWarningAsync(
                    "Ort nicht gefunden. Bitte pruefe die Schreibweise und versuche es erneut.");
                return;
            }

            MainMap.MoveToRegion(MapSpan.FromCenterAndRadius(
                new Location(location.Latitude, location.Longitude),
                Distance.FromKilometers(2)));

            await ReloadForCurrentViewportAsync(force: true);
        }

        private async void OnFilterClicked(object sender, EventArgs e)
        {
            var selected = await Shell.Current.DisplayActionSheetAsync(
                "Kategorie filtern",
                "Abbrechen",
                null,
                "Alles",
                "Anfrage",
                "Angebot",
                "Event");

            if (selected == null || selected == "Abbrechen")
            {
                return;
            }

            await _viewModel.SetCategoryAsync(selected, MainMap.VisibleRegion);
            RenderPins();
        }

        private async void OnResetFilterClicked(object sender, EventArgs e)
        {
            await _viewModel.ResetCategoryAsync(MainMap.VisibleRegion);
            RenderPins();
        }

        private void OnMapCardChanged(object? sender, CurrentItemChangedEventArgs e)
        {
            if (e.CurrentItem is not MapEntryPinItem selected)
            {
                return;
            }

            MainMap.MoveToRegion(MapSpan.FromCenterAndRadius(
                new Location(selected.Latitude, selected.Longitude),
                Distance.FromKilometers(1.2)));
        }

        private async void OnMapCardTapped(object? sender, TappedEventArgs e)
        {
            if (sender is not BindableObject bindable || bindable.BindingContext is not MapEntryPinItem item)
            {
                return;
            }

            await Shell.Current.GoToAsync($"{nameof(DetailPage)}?EntryId={item.Id}");
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MapPageViewModel.IsLoading))
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    LoadingIndicator.IsVisible = _viewModel.IsLoading;
                });
            }
        }

        private async void OnMapPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != "VisibleRegion")
            {
                return;
            }

            _viewportReloadDebounceCts?.Cancel();
            _viewportReloadDebounceCts = new CancellationTokenSource();
            var token = _viewportReloadDebounceCts.Token;

            try
            {
                await Task.Delay(450, token);
                if (token.IsCancellationRequested)
                {
                    return;
                }

                await ReloadForCurrentViewportAsync(force: false);
            }
            catch (TaskCanceledException)
            {
                // debounce
            }
        }
    }
}

