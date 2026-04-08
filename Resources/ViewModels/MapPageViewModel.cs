using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.Maps;
using NBH.Resources.Models;
using Newtonsoft.Json;
using static Supabase.Postgrest.Constants;

namespace NBH.Resources.ViewModels
{
    public partial class MapPageViewModel : ObservableObject
    {
        private readonly Supabase.Client _client;
        private readonly ILogger<MapPageViewModel> _logger;
        private readonly SemaphoreSlim _loadLock = new(1, 1);
        private readonly Dictionary<string, Location> _geocodeCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _failedGeocodeLocations = new(StringComparer.OrdinalIgnoreCase);

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private string _selectedCategory = "Alles";

        public ObservableCollection<MapEntryPinItem> Pins { get; } = new();

        private DateTime _lastViewportReloadAt = DateTime.MinValue;

        public MapPageViewModel(Supabase.Client client, ILogger<MapPageViewModel> logger)
        {
            _client = client;
            _logger = logger;
        }

        public async Task InitializeAsync()
        {
            await ReloadForViewportAsync(null, force: true);
        }

        public async Task SetCategoryAsync(string category, MapSpan? currentViewport)
        {
            SelectedCategory = string.IsNullOrWhiteSpace(category) ? "Alles" : category;
            await ReloadForViewportAsync(currentViewport, force: true);
        }

        public async Task ResetCategoryAsync(MapSpan? currentViewport)
        {
            SelectedCategory = "Alles";
            await ReloadForViewportAsync(currentViewport, force: true);
        }

        public async Task ReloadForViewportAsync(MapSpan? visibleRegion, bool force = false)
        {
            if (!force && (DateTime.UtcNow - _lastViewportReloadAt).TotalMilliseconds < 750)
            {
                return;
            }

            var bounds = BuildBounds(visibleRegion);
            await LoadPinsAsync(bounds.minLat, bounds.maxLat, bounds.minLng, bounds.maxLng, SelectedCategory);
            _lastViewportReloadAt = DateTime.UtcNow;
        }

        public async Task<Location?> TryGetCurrentLocationAsync()
        {
            var status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            if (status != PermissionStatus.Granted)
            {
                return null;
            }

            var location = await Geolocation.GetLastKnownLocationAsync();
            if (location != null)
            {
                return location;
            }

            var request = new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(7));
            return await Geolocation.GetLocationAsync(request);
        }

        public async Task<Location?> SearchLocationAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return null;
            }

            var results = await Geocoding.GetLocationsAsync(query.Trim());
            return results?.FirstOrDefault();
        }

        private async Task LoadPinsAsync(
            double minLat,
            double maxLat,
            double minLng,
            double maxLng,
            string category)
        {
            await _loadLock.WaitAsync();
            IsLoading = true;
            try
            {
                var rpcItems = await QueryMapEntriesRpcAsync(
                    minLat,
                    maxLat,
                    minLng,
                    maxLng,
                    category).ConfigureAwait(false);

                // Wenn RPC leer zurückgibt, fallback auf direkte entries + gecachte Geokodierung.
                if (rpcItems.Count == 0)
                {
                    _logger.LogWarning("Map RPC liefert keine Pins. Starte Fallback-Ladung aus entries + Geocoding.");
                    rpcItems = await LoadFallbackPinsFromEntriesAsync(
                        minLat,
                        maxLat,
                        minLng,
                        maxLng,
                        category).ConfigureAwait(false);
                }

                var filtered = rpcItems
                    .Where(x => x.Latitude is >= -90 and <= 90 && x.Longitude is >= -180 and <= 180)
                    .GroupBy(x => x.Id)
                    .Select(g => g.First())
                    .ToList();

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    Pins.Clear();
                    foreach (var item in filtered)
                    {
                        Pins.Add(item);
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MapPageViewModel: Fehler beim Laden der Kartenpins.");
            }
            finally
            {
                IsLoading = false;
                _loadLock.Release();
            }
        }

        private async Task<List<MapEntryPinItem>> QueryMapEntriesRpcAsync(
            double minLat,
            double maxLat,
            double minLng,
            double maxLng,
            string category)
        {
            async Task<List<MapEntryPinItem>> QueryAsync(
                double qMinLat,
                double qMaxLat,
                double qMinLng,
                double qMaxLng,
                string qCategory)
            {
                var response = await _client.Rpc("get_map_entries", new Dictionary<string, object>
                {
                    { "p_min_lat", qMinLat },
                    { "p_max_lat", qMaxLat },
                    { "p_min_lng", qMinLng },
                    { "p_max_lng", qMaxLng },
                    { "p_category", qCategory }
                }).ConfigureAwait(false);

                return string.IsNullOrWhiteSpace(response?.Content)
                    ? new List<MapEntryPinItem>()
                    : JsonConvert.DeserializeObject<List<MapEntryPinItem>>(response.Content) ?? new List<MapEntryPinItem>();
            }

            var categoryVariants = string.Equals(category, "Alles", StringComparison.OrdinalIgnoreCase)
                ? new[] { "Alles", "all", "Allgemein", "ALL" }
                : new[] { category };

            foreach (var categoryVariant in categoryVariants)
            {
                var items = await QueryAsync(minLat, maxLat, minLng, maxLng, categoryVariant).ConfigureAwait(false);
                if (items.Count > 0)
                {
                    return items;
                }

                if (minLat > -90 || maxLat < 90 || minLng > -180 || maxLng < 180)
                {
                    items = await QueryAsync(-90, 90, -180, 180, categoryVariant).ConfigureAwait(false);
                    if (items.Count > 0)
                    {
                        return items;
                    }
                }
            }

            return new List<MapEntryPinItem>();
        }

        private async Task<List<MapEntryPinItem>> LoadFallbackPinsFromEntriesAsync(
            double minLat,
            double maxLat,
            double minLng,
            double maxLng,
            string category)
        {
            var query = _client
                .From<MapFallbackEntry>()
                .Filter("is_completed", Operator.Equals, "false")
                .Order(x => x.created_date, Supabase.Postgrest.Constants.Ordering.Descending)
                .Limit(120);

            if (!string.Equals(category, "Alles", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Filter("category", Operator.Equals, category);
            }

            var response = await query.Get().ConfigureAwait(false);
            var rows = response.Models ?? new List<MapFallbackEntry>();
            var result = new List<MapEntryPinItem>();

            foreach (var row in rows)
            {
                if (row.id == Guid.Empty)
                {
                    continue;
                }

                var location = await ResolveEntryLocationAsync(row).ConfigureAwait(false);
                if (location == null)
                {
                    continue;
                }

                if (location.Latitude < minLat || location.Latitude > maxLat || location.Longitude < minLng || location.Longitude > maxLng)
                {
                    continue;
                }

                result.Add(new MapEntryPinItem
                {
                    Id = row.id,
                    Title = row.title,
                    Category = row.category,
                    Location = row.location,
                    Latitude = location.Latitude,
                    Longitude = location.Longitude,
                    EventType = row.event_type,
                    UserId = row.user_id
                });
            }

            return result;
        }

        private async Task<Location?> ResolveEntryLocationAsync(MapFallbackEntry row)
        {
            if (row.latitude is >= -90 and <= 90 && row.longitude is >= -180 and <= 180)
            {
                return new Location(row.latitude!.Value, row.longitude!.Value);
            }

            var rawLocation = row.location?.Trim();
            if (string.IsNullOrWhiteSpace(rawLocation))
            {
                return null;
            }

            if (_geocodeCache.TryGetValue(rawLocation, out var cached))
            {
                return cached;
            }

            if (_failedGeocodeLocations.Contains(rawLocation))
            {
                return null;
            }

            try
            {
                var geoResults = await Geocoding.GetLocationsAsync(rawLocation).ConfigureAwait(false);
                var first = geoResults?.FirstOrDefault();
                if (first == null)
                {
                    _failedGeocodeLocations.Add(rawLocation);
                    return null;
                }

                _geocodeCache[rawLocation] = first;
                return first;
            }
            catch
            {
                _failedGeocodeLocations.Add(rawLocation);
                return null;
            }
        }

        private static (double minLat, double maxLat, double minLng, double maxLng) BuildBounds(MapSpan? visibleRegion)
        {
            if (visibleRegion == null)
            {
                return (-90, 90, -180, 180);
            }

            // Viewport etwas erweitern, damit bei kleinen Zoomstufen nicht zu aggressiv
            // gefiltert wird und "nahe" Pins weiterhin sichtbar bleiben.
            const double minHalfSpan = 0.08; // ~8-9km je nach Breite
            const double viewportPaddingFactor = 1.35;

            var halfLat = Math.Max((visibleRegion.LatitudeDegrees / 2.0) * viewportPaddingFactor, minHalfSpan);
            var halfLng = Math.Max((visibleRegion.LongitudeDegrees / 2.0) * viewportPaddingFactor, minHalfSpan);

            var minLat = Clamp(visibleRegion.Center.Latitude - halfLat, -90, 90);
            var maxLat = Clamp(visibleRegion.Center.Latitude + halfLat, -90, 90);
            var minLng = Clamp(visibleRegion.Center.Longitude - halfLng, -180, 180);
            var maxLng = Clamp(visibleRegion.Center.Longitude + halfLng, -180, 180);

            return (minLat, maxLat, minLng, maxLng);
        }

        private static double Clamp(double value, double min, double max)
            => Math.Min(max, Math.Max(min, value));

        [Supabase.Postgrest.Attributes.Table("entries")]
        private class MapFallbackEntry : Supabase.Postgrest.Models.BaseModel
        {
            [Supabase.Postgrest.Attributes.PrimaryKey("id", false)]
            public Guid id { get; set; }

            [Supabase.Postgrest.Attributes.Column("title")]
            public string title { get; set; } = string.Empty;

            [Supabase.Postgrest.Attributes.Column("category")]
            public string category { get; set; } = string.Empty;

            [Supabase.Postgrest.Attributes.Column("location")]
            public string? location { get; set; }

            [Supabase.Postgrest.Attributes.Column("latitude")]
            public double? latitude { get; set; }

            [Supabase.Postgrest.Attributes.Column("longitude")]
            public double? longitude { get; set; }

            [Supabase.Postgrest.Attributes.Column("event_type")]
            public string event_type { get; set; } = "single";

            [Supabase.Postgrest.Attributes.Column("user_id")]
            public Guid user_id { get; set; }

            [Supabase.Postgrest.Attributes.Column("created_date")]
            public DateTime created_date { get; set; }
        }
    }
}
