using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.Maps;
using NBH.Resources.Models;
using NBH.Services;
using Supabase;
using Microsoft.Extensions.Logging;
using NBH.Resources.Views;
#if IOS
using NBH.Platforms.iOS;
#endif

namespace NBH
{
    public partial class MapPage : ContentPage
    {
        private readonly Client _client;
        private readonly ILogger<MapPage> _logger;

        public MapPage(Client client, ILogger<MapPage> logger)
        {
            InitializeComponent();
            _client = client;
            _logger = logger;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            try
            {
                MyTabBar.SetActiveTabWithoutNavigation(1);  // 1 = Karte
                MyBell.OnNewNotificationReceived();

                // 1) OPTIONAL: aktuellen Standort zentrieren
                await LoadCurrentLocation();

                // 2) Pins laden, damit Einträge mit Location angezeigt werden
                await LoadPinsFromDatabaseAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MapPage] OnAppearing Fehler: {ex.Message}");
            }
        }

        /// <summary>
        /// Lädt alle Einträge aus der DB, die eine Location haben,
        /// und setzt Pins auf der Karte.
        /// </summary>
        private async Task LoadPinsFromDatabaseAsync()
        {
            try
            {
                _logger.LogInformation("MapPage: Lade alle Einträge aus DB, um Pins zu setzen.");

                var response = await _client.From<EntryItem>().Get();
                if (response != null && response.Models.Any())
                {
                    // Vorher ggf. alte Pins entfernen, um Duplikate zu vermeiden
                    MainMap.Pins.Clear();

                    foreach (var entry in response.Models)
                    {
                        if (!string.IsNullOrWhiteSpace(entry.location))
                        {
                            var locations = await Geocoding.GetLocationsAsync(entry.location);
                            var first = locations?.FirstOrDefault();
                            if (first != null)
                            {
                                AddPin(first.Latitude, first.Longitude, entry);
                                _logger.LogInformation($"Pin für {entry.title} auf {entry.location} gesetzt.");
                            }
                            else
                            {
                                _logger.LogWarning($"Geocoding für '{entry.location}' ergab keine Koordinaten.");
                            }
                        }
                    }
                }
                else
                {
                    _logger.LogWarning("MapPage: Keine Einträge oder leere DB-Antwort.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Laden der Pins aus der DB.");
            }
        }

        /// <summary>
        /// Fügt einen Pin zur Karte hinzu und leitet bei Klick zur DetailPage mit dem EntryItem weiter.
        /// </summary>
        public void AddPin(double latitude, double longitude, EntryItem entry)
        {
            try
            {
                if (MainMap != null)
                {
                    _logger.LogInformation($"AddPin: {entry.title} (Lat={latitude}, Lon={longitude}).");
                    var pin = new Pin
                    {
                        Label = entry.title,
                        Location = new Location(latitude, longitude),
                        Type = PinType.Place
                    };

                    // Klick-Event => Weiterleitung zur DetailPage mit dem EntryItem
                    pin.MarkerClicked += async (s, e) =>
                    {
                        e.HideInfoWindow = true; // InfoWindow ausblenden, falls gewünscht
                        // Navigation zur DetailPage
                        await Shell.Current.GoToAsync(
                            $"{nameof(DetailPage)}",
                            true,
                            new Dictionary<string, object>
                            {
                                { "SelectedEntry", entry }
                            });
                    };

                    MainMap.Pins.Add(pin);
                }
                else
                {
                    _logger.LogWarning("AddPin: MainMap ist null.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Hinzufügen eines Pins.");
            }
        }

        /// <summary>
        /// Wird beim Klick auf den "Suchen"-Button aufgerufen.
        /// </summary>
        private async void OnSearchClicked(object sender, EventArgs e)
        {
            try
            {
#if IOS
                iOSHapticFeedback.TriggerHeavyImpact();
#endif
                HapticFeedback.Perform(HapticFeedbackType.Click);
                var searchQuery = SearchEntry.Text;

                if (!string.IsNullOrWhiteSpace(searchQuery))
                {
                    var locations = await Geocoding.GetLocationsAsync(searchQuery);
                    var location = locations?.FirstOrDefault();

                    if (location != null)
                    {
                        // Karte zentrieren
                        MainMap.MoveToRegion(MapSpan.FromCenterAndRadius(
                            new Location(location.Latitude, location.Longitude),
                            Distance.FromKilometers(2)));
                    }
                    else
                    {
#if IOS
                        iOSHapticFeedback.TriggerHeavyImpact();
#endif
                        HapticFeedback.Perform(HapticFeedbackType.Click);
                        await ToastService.ShowWarningAsync("Ort nicht gefunden. Bitte prüfe die Schreibweise und versuche es erneut.");
                    }
                }
                else
                {
#if IOS
                    iOSHapticFeedback.TriggerHeavyImpact();
#endif
                    HapticFeedback.Perform(HapticFeedbackType.Click);
                    await ToastService.ShowWarningAsync("Bitte gib einen gültigen Ort ein.");
                }
            }
            catch (Exception ex)
            {
#if IOS
                iOSHapticFeedback.TriggerHeavyImpact();
#endif
                HapticFeedback.Perform(HapticFeedbackType.Click);
                await ToastService.ShowErrorAsync(UserFriendlyErrors.GetMessage(ex));
            }
        }

        /// <summary>
        /// Lädt den aktuellen Standort und fügt optional einen Pin hinzu.
        /// </summary>
        private async void OnLoadCurrentLocationClicked(object sender, EventArgs e) { await LoadCurrentLocation(); }

        private async Task LoadCurrentLocation()
        {
            try
            {
                _logger.LogInformation("LoadCurrentLocation: Lade aktuellen Standort...");

                HapticFeedback.Perform(HapticFeedbackType.Click);
                var status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
                if (status != PermissionStatus.Granted)
                {
#if IOS
                    iOSHapticFeedback.TriggerHeavyImpact();
#endif
                    HapticFeedback.Perform(HapticFeedbackType.Click);
                    await ToastService.ShowWarningAsync("Standortberechtigung benötigt. Bitte aktiviere den Standort in den Einstellungen.");
                    _logger.LogWarning("LoadCurrentLocation: Berechtigung nicht erteilt.");
                    return;
                }

                var location = await Geolocation.GetLastKnownLocationAsync();
                if (location != null)
                {
                    _logger.LogInformation($"LoadCurrentLocation: Standort = {location.Latitude}, {location.Longitude}");

                    MainMap.MoveToRegion(MapSpan.FromCenterAndRadius(
                        new Location(location.Latitude, location.Longitude),
                        Distance.FromKilometers(5)));

                    // Optional: Pin für "Mein Standort" hinzufügen
                    AddPin(location.Latitude, location.Longitude, new EntryItem
                    {
                        title = "Mein Standort",
                        location = $"{location.Latitude},{location.Longitude}"
                    });
                }
                else
                {
                    _logger.LogWarning("LoadCurrentLocation: Kein Standort verfügbar.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Laden des aktuellen Standorts.");
#if IOS
                iOSHapticFeedback.TriggerHeavyImpact();
#endif
                HapticFeedback.Perform(HapticFeedbackType.Click);
                await ToastService.ShowErrorAsync("Standort konnte nicht geladen werden. Bitte prüfe deine Standortberechtigung.");
            }
        }
    }
}

