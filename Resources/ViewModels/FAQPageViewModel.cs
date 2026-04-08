using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NBH.Resources.Models;
using Supabase;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace NBH.Resources.ViewModels
{
    public partial class FAQPageViewModel : ObservableObject
    {
        private readonly Client _supabaseClient;
        private static readonly HashSet<string> LocalFaqAssetFileNames = new(StringComparer.OrdinalIgnoreCase)
        {
            // FAQ screenshots that are bundled with the app.
            "ap_beschreibung.png",
            "ap_entry.png",
            "ap_filter.png",
            "ap_hinzufuegen.png",
            "ap_kategorie.png"
        };

        [ObservableProperty]
        private ObservableCollection<FAQCategory> _categories = new();

        [ObservableProperty]
        private ObservableCollection<FAQCategory> _visibleCategories = new();

        [ObservableProperty]
        private ObservableCollection<string> _categoryChips = new();

        [ObservableProperty]
        private string _selectedCategoryChip = "Alle";

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private string _errorMessage = string.Empty;

        public IAsyncRelayCommand LoadFAQCommand { get; }
        public IRelayCommand<string> SelectCategoryChipCommand { get; }

        public FAQPageViewModel(Client supabaseClient)
        {
            _supabaseClient = supabaseClient;
            LoadFAQCommand = new AsyncRelayCommand(LoadFAQFromDBAsync);
            SelectCategoryChipCommand = new RelayCommand<string>(SelectCategoryChip);
        }

        private async Task LoadFAQFromDBAsync()
        {
            if (IsLoading)
                return;

            IsLoading = true;
            ErrorMessage = string.Empty;
            var loadingStartedAt = DateTime.UtcNow;

            try
            {
                var rows = await LoadModernFaqAsync();
                if (!rows.Any() || LooksLikeCollapsedFaq(rows))
                {
                    rows = await LoadLegacyFaqFallbackAsync();
                }

                if (!rows.Any())
                {
                    ErrorMessage = "Keine FAQ-Einträge gefunden.";
                    return;
                }

                NormalizeImageLinks(rows);

                var grouped = rows
                    .GroupBy(r => string.IsNullOrWhiteSpace(r.CategoryTitle) ? "Allgemein" : r.CategoryTitle)
                    .Select(g => new FAQCategory
                    {
                        CategoryTitle = g.Key,
                        Items = g.OrderBy(x => x.SortOrder).ToList()
                    })
                    .ToList();

                Categories.Clear();
                foreach (var category in grouped)
                {
                    Categories.Add(category);
                }

                RebuildCategoryChips();
                ApplyCategoryFilter();
            }
            catch (Exception ex)
            {
                ErrorMessage = "Fehler beim Laden der FAQ.";
                System.Diagnostics.Debug.WriteLine($"[FAQ ERROR] {ex.Message}");
            }
            finally
            {
                var elapsed = DateTime.UtcNow - loadingStartedAt;
                var minLoadingDuration = TimeSpan.FromMilliseconds(900);
                if (elapsed < minLoadingDuration)
                {
                    await Task.Delay(minLoadingDuration - elapsed);
                }
                IsLoading = false;
            }
        }

        private async Task<List<FAQRow>> LoadModernFaqAsync()
        {
            var response = await _supabaseClient.From<FAQRow>()
                .Order(f => f.SortOrder, Supabase.Postgrest.Constants.Ordering.Ascending)
                .Get();

            return response.Models?.ToList() ?? new List<FAQRow>();
        }

        private async Task<List<FAQRow>> LoadLegacyFaqFallbackAsync()
        {
            try
            {
                var response = await _supabaseClient.From<LegacyFAQRow>()
                    .Order(f => f.SortOrder, Supabase.Postgrest.Constants.Ordering.Ascending)
                    .Get();

                return response.Models?
                    .Select(x => new FAQRow
                    {
                        Id = x.Id,
                        CategoryTitle = string.IsNullOrWhiteSpace(x.Category) ? "Allgemein" : x.Category,
                        Subtitle = x.Question ?? string.Empty,
                        Content = x.Answer ?? string.Empty,
                        SortOrder = x.SortOrder,
                        ImageUrl = string.Empty
                    })
                    .ToList() ?? new List<FAQRow>();
            }
            catch
            {
                return new List<FAQRow>();
            }
        }

        private static bool LooksLikeCollapsedFaq(IEnumerable<FAQRow> rows)
        {
            var materialized = rows.ToList();
            if (!materialized.Any())
                return true;

            var averageContentLength = materialized
                .Where(x => !string.IsNullOrWhiteSpace(x.Content))
                .Select(x => x.Content!.Trim().Length)
                .DefaultIfEmpty(0)
                .Average();

            return averageContentLength > 0 && averageContentLength < 80;
        }

        private static void NormalizeImageLinks(IEnumerable<FAQRow> rows)
        {
            var rowList = rows.ToList();
            var baseUrl = BuildFaqImageBaseUrl(rowList);

            // Replace markdown images with relative sources:
            // ![Alt](ap_filter.png) -> ![Alt](https://.../faq_bilder/ap_filter.png)
            var markdownImageRegex = new Regex(@"!\[(?<alt>[^\]]*)\]\((?<url>[^)]+)\)", RegexOptions.Compiled);

            foreach (var row in rowList)
            {
                if (string.IsNullOrWhiteSpace(row.Content))
                {
                    row.Content = string.Empty;
                }
                else
                {
                    row.Content = SanitizeCalloutBlocks(row.Content);
                }

                row.Content = markdownImageRegex.Replace(row.Content, match =>
                {
                    var alt = match.Groups["alt"].Value;
                    var url = match.Groups["url"].Value.Trim();
                    var resolved = ResolveFaqImageUrl(url, baseUrl);
                    return $"![{alt}]({resolved})";
                });

                if (!string.IsNullOrWhiteSpace(row.ImageUrl))
                {
                    row.ImageUrl = ResolveFaqImageUrl(row.ImageUrl.Trim(), baseUrl);
                }
            }
        }

        private static string ResolveFaqImageUrl(string rawValue, string baseUrl)
        {
            var candidate = rawValue?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(candidate))
            {
                return string.Empty;
            }

            // Best performance path: use bundled image asset when known.
            var localAsset = TryResolveLocalFaqAsset(candidate);
            if (!string.IsNullOrWhiteSpace(localAsset))
            {
                return localAsset;
            }

            if (candidate.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                candidate.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return NormalizeAbsoluteStorageUrl(candidate, baseUrl);
            }

            // Keep app-local resource references untouched.
            if (candidate.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }

            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                return candidate;
            }

            return $"{baseUrl}{EncodeRelativePath(candidate)}";
        }

        private static string? TryResolveLocalFaqAsset(string rawValue)
        {
            var fileName = ExtractFileNameFromPathOrUrl(rawValue);
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return null;
            }

            return LocalFaqAssetFileNames.Contains(fileName) ? fileName : null;
        }

        private static string? ExtractFileNameFromPathOrUrl(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var cleaned = value.Trim();
            if (Uri.TryCreate(cleaned, UriKind.Absolute, out var absolute))
            {
                var unescaped = Uri.UnescapeDataString(absolute.AbsolutePath);
                return Path.GetFileName(unescaped);
            }

            cleaned = cleaned.Replace("\\", "/");
            var q = cleaned.IndexOf('?');
            if (q >= 0)
            {
                cleaned = cleaned[..q];
            }
            return Path.GetFileName(cleaned);
        }

        private static string BuildFaqImageBaseUrl(IEnumerable<FAQRow> rows)
        {
            var supabaseUrl = AppConfig.SupabaseUrl?.Trim();
            if (string.IsNullOrWhiteSpace(supabaseUrl) || !supabaseUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            var discoveredBucket = rows
                .SelectMany(r => new[] { r.ImageUrl, r.Content })
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Select(ExtractPublicBucketName)
                .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

            var bucket = string.IsNullOrWhiteSpace(discoveredBucket) ? "faq_bilder" : discoveredBucket!;
            return $"{supabaseUrl.TrimEnd('/')}/storage/v1/object/public/{bucket}/";
        }

        private static string? ExtractPublicBucketName(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            var match = Regex.Match(
                text,
                @"/storage/v1/object/public/(?<bucket>[^/\s)]+)/",
                RegexOptions.IgnoreCase);

            return match.Success ? match.Groups["bucket"].Value : null;
        }

        private static string EncodeRelativePath(string relativePath)
        {
            var cleaned = relativePath.Trim().TrimStart('/').Replace("\\", "/");
            if (string.IsNullOrWhiteSpace(cleaned))
            {
                return string.Empty;
            }

            var parts = cleaned
                .Split('/', StringSplitOptions.RemoveEmptyEntries)
                .Select(Uri.EscapeDataString);
            return string.Join("/", parts);
        }

        private static string NormalizeAbsoluteStorageUrl(string url, string baseUrl)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return url;
            }

            // Handle malformed absolute paths that start with /storage/...
            if (url.StartsWith("/storage/", StringComparison.OrdinalIgnoreCase))
            {
                if (Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri))
                {
                    return $"{baseUri.Scheme}://{baseUri.Host}{url}";
                }
            }

            if (!Uri.TryCreate(url, UriKind.Absolute, out var abs))
            {
                return url;
            }

            var encodedPath = string.Join("/",
                abs.AbsolutePath
                   .Split('/', StringSplitOptions.RemoveEmptyEntries)
                   .Select(Uri.EscapeDataString));

            var encoded = $"{abs.Scheme}://{abs.Host}/{encodedPath}";
            if (!string.IsNullOrWhiteSpace(abs.Query))
            {
                encoded += abs.Query;
            }
            return encoded;
        }

        private static string SanitizeCalloutBlocks(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return string.Empty;
            }

            // Convert Markdig callout syntax ([!NOTE], [!WARNING], ...) to plain text
            // to avoid renderer-specific local icon lookups like "icon.png".
            var replaced = Regex.Replace(
                content,
                @"(?im)^\s*>\s*\[\!(?<type>[A-Z]+)\]\s*$",
                m => $"> Hinweis ({m.Groups["type"].Value}):");

            var builder = new StringBuilder(replaced.Length);
            foreach (var line in replaced.Split('\n'))
            {
                builder.AppendLine(line.TrimEnd('\r'));
            }

            return builder.ToString().TrimEnd();
        }

        private void RebuildCategoryChips()
        {
            CategoryChips.Clear();
            CategoryChips.Add("Alle");
            foreach (var category in Categories.Select(x => x.CategoryTitle).Distinct())
            {
                CategoryChips.Add(category);
            }
        }

        private void SelectCategoryChip(string? chip)
        {
            if (string.IsNullOrWhiteSpace(chip))
            {
                return;
            }

            SelectedCategoryChip = chip;
            ApplyCategoryFilter();
        }

        private void ApplyCategoryFilter()
        {
            VisibleCategories.Clear();

            var filtered = SelectedCategoryChip == "Alle"
                ? Categories
                : new ObservableCollection<FAQCategory>(
                    Categories.Where(x => string.Equals(x.CategoryTitle, SelectedCategoryChip, StringComparison.OrdinalIgnoreCase)));

            foreach (var category in filtered)
            {
                VisibleCategories.Add(category);
            }
        }
    }
}
