using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using Supabase;
using NBH.Resources.Models;
using NBH.Services;
using Newtonsoft.Json;
#if IOS
using NBH.Platforms.iOS;
#endif

namespace NBH.Resources.ViewModels
{
    public partial class AddPageViewModel : INotifyPropertyChanged
    {
        private readonly Supabase.Client _client;
        private readonly ILogger<AddPageViewModel> _logger;
        private readonly AuthService _authService;
        private readonly AppSessionFlowService _appSessionFlowService;

        // Anzahl EintrÃ¤ge pro Seite
        private const int ItemsPerPage = 10;

        // Interne Liste (gefiltert), aus der die Paginierung bedient wird
        private List<EntryItem> _filteredAll = new();

        private int _currentPage = 1;
        public int CurrentPage
        {
            get => _currentPage;
            set
            {
                _currentPage = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PageDisplay));
            }
        }

        // Gesamte Seitenanzahl = RoundUp aus Anzahl gefilterter EintrÃ¤ge / ItemsPerPage

        public string PageDisplay => $"Seite {CurrentPage} von {Math.Max(TotalPagesCount, 1)}";

        // Hauptliste (alle EintrÃ¤ge) und gefilterte/paginierte Liste fÃ¼rs UI
        public ObservableCollection<EntryItem> Entries { get; private set; } = new();
        public ObservableCollection<EntryItem> FilteredEntries { get; private set; } = new();
        public ObservableCollection<EntryItem> FeaturedEntries { get; private set; } = new();
        public bool HasFeaturedEntries => FeaturedEntries.Count > 0;
        public bool ShowFeaturedSection => HasFeaturedEntries && IsFeaturedSectionExpanded;

        public ObservableCollection<string> Categories { get; private set; } = new() { "Anfrage", "Angebot" };
        public ObservableCollection<string> FilterOptions { get; private set; } = new() { "Alles", "Anfrage", "Angebot" };
        public ObservableCollection<string> EventTypeFilterOptions { get; private set; } = new() { "Alle", "Einzel", "Gruppe" };

        // Kombinierter Filter (ersetzt die zwei getrennten Picker)
        public ObservableCollection<string> CombinedFilterOptions { get; private set; } = new()
        {
            "Alle Einträge",
            "Anfragen",
            "Angebote",
            "Einzel-Events",
            "Gruppen-Events"
        };

        private string _selectedCombinedFilter = "Alle Einträge";
        public string SelectedCombinedFilter
        {
            get => _selectedCombinedFilter;
            set
            {
                if (_selectedCombinedFilter == value) return;
                _selectedCombinedFilter = value;
                OnPropertyChanged();
                // Auf interne Einzelfilter mappen
                switch (value)
                {
                    case "Anfragen":       _selectedFilter = "Anfrage";  _selectedEventTypeFilter = "Alle";   break;
                    case "Angebote":       _selectedFilter = "Angebot";  _selectedEventTypeFilter = "Alle";   break;
                    case "Einzel-Events":  _selectedFilter = "Alles";    _selectedEventTypeFilter = "Einzel"; break;
                    case "Gruppen-Events": _selectedFilter = "Alles";    _selectedEventTypeFilter = "Gruppe"; break;
                    default:               _selectedFilter = "Alles";    _selectedEventTypeFilter = "Alle";   break;
                }
                _ = LoadEntriesFromDatabaseAsync(1);
            }
        }

        private bool _isRefreshing;
        public bool IsRefreshing
        {
            get => _isRefreshing;
            set { _isRefreshing = value; OnPropertyChanged(); }
        }        private string _selectedFilter = "Alles";
        public string SelectedFilter
        {
            get => _selectedFilter;
            set
            {
                if (_selectedFilter != value)
                {
                    _selectedFilter = value;
                    OnPropertyChanged();
                    _ = LoadEntriesFromDatabaseAsync(1);
                }
            }
        }

        private string _selectedEventTypeFilter = "Alle";
        public string SelectedEventTypeFilter
        {
            get => _selectedEventTypeFilter;
            set
            {
                if (_selectedEventTypeFilter != value)
                {
                    _selectedEventTypeFilter = value;
                    OnPropertyChanged();
                    _ = LoadEntriesFromDatabaseAsync(1);
                }
            }
        }

        // Commands
        public ICommand RefreshCommand { get; private set; }
        public ICommand OpenDetailCommand { get; private set; }
        public ICommand CompleteEntryCommand { get; private set; }
        public ICommand NextPageCommand { get; private set; }
        public ICommand PrevPageCommand { get; private set; }
        public ICommand FirstPageCommand { get; private set; }
        public ICommand LastPageCommand { get; private set; }

        public ICommand NavigateToCreateCommand { get; private set; }
        public ICommand ToggleFeaturedSectionCommand { get; private set; }

        private bool _isFeaturedSectionExpanded = true;
        public bool IsFeaturedSectionExpanded
        {
            get => _isFeaturedSectionExpanded;
            set
            {
                if (_isFeaturedSectionExpanded == value)
                {
                    return;
                }

                _isFeaturedSectionExpanded = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ShowFeaturedSection));
                OnPropertyChanged(nameof(FeaturedToggleText));
            }
        }

        public string FeaturedToggleText => IsFeaturedSectionExpanded ? "Highlights ausblenden" : "Highlights anzeigen";

        private bool _isLoading;
        public bool IsLoading { get => _isLoading; set { _isLoading = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsNotLoading)); } }
        public bool IsNotLoading => !IsLoading;

        /// <summary>
        /// Leerer Konstruktor â€“ nur fÃ¼r XAML-Preview / Designer (falls gewÃ¼nscht).
        /// Rufe hier eine minimale Initialisierung der Commands auf, damit
        /// der Designer nicht abstÃ¼rzt.
        /// </summary>
        public AddPageViewModel()
        {
            InitializeCommands();
        }

        /// <summary>
        /// Konstruktor mit Dependency Injection.
        /// </summary>
        /// <param name="client">Supabase-Client</param>
        /// <param name="logger">Logger</param>
        /// <param name="authService">Auth-Service</param>
        public AddPageViewModel(
            Supabase.Client client,
            ILogger<AddPageViewModel> logger,
            AuthService authService,
            AppSessionFlowService appSessionFlowService)
            : this()
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            _appSessionFlowService = appSessionFlowService ?? throw new ArgumentNullException(nameof(appSessionFlowService));
        }

        /// <summary>
        /// Initialisiere alle Commands. Wird sowohl im parameterlosen Konstruktor
        /// als auch im DI-Konstruktor aufgerufen.
        /// </summary>
        private void InitializeCommands()
        {
            RefreshCommand = new Command(async () => await RefreshEntriesAsync());
            OpenDetailCommand = new Command<EntryItem>(OpenDetail);
            CompleteEntryCommand = new Command<EntryItem>(async (entry) => await CompleteEntryAsync(entry));

            NextPageCommand = new Command(async () => await LoadEntriesFromDatabaseAsync(CurrentPage + 1), () => CurrentPage < TotalPagesCount);
            PrevPageCommand = new Command(async () => await LoadEntriesFromDatabaseAsync(CurrentPage - 1), () => CurrentPage > 1);
            FirstPageCommand = new Command(async () => await LoadEntriesFromDatabaseAsync(1), () => CurrentPage > 1);
            LastPageCommand = new Command(async () => await LoadEntriesFromDatabaseAsync(TotalPagesCount), () => CurrentPage < TotalPagesCount);
            NavigateToCreateCommand = new Command(async () => await Shell.Current.GoToAsync("CreateEventPage"));
            ToggleFeaturedSectionCommand = new Command(() => IsFeaturedSectionExpanded = !IsFeaturedSectionExpanded);
        }

        // -----------------------------------------
        // LOAD Page FROM DB (Server-Side)
        // -----------------------------------------
        public async Task LoadEntriesFromDatabaseAsync(int page = 1)
        {
            try
            {
                IsLoading = true;
                if (!await _authService.EnsureSessionValidAsync())
                {
                    await _appSessionFlowService.NavigateToLoginAsync(
                        UserFriendlyErrors.Messages.SessionExpired);
                    return;
                }

                CurrentPage = page;
                var skip = (CurrentPage - 1) * ItemsPerPage;

                _logger?.LogInformation("Lade Seite {Page} (Skip: {Skip}) via RPC...", CurrentPage, skip);

                // RPC Aufruf an Postgres
                var rpcParams = new Dictionary<string, object>
                {
                    { "p_category", SelectedFilter },
                    { "p_event_type", SelectedEventTypeFilter },
                    { "p_limit", ItemsPerPage },
                    { "p_offset", skip }
                };

                var response = await _client.Rpc("get_entries_paged_v2", rpcParams);

                if (response != null && response.Content != null)
                {
                    // Deserialisierung der RPC-Ergebnisse (JSON)
                    var pageResponse = JsonConvert.DeserializeObject<EntriesPageRpcResponse>(response.Content);
                    var items = pageResponse?.Entries ?? new List<EntryItem>();
                    var total = Math.Max(pageResponse?.Total ?? 0, 0);
                    
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        FilteredEntries.Clear();
                        foreach (var item in items)
                        {
                            FilteredEntries.Add(item);
                        }
                        RebuildFeaturedEntries();
                        UpdatePaginationButtons();
                        // Da wir nicht wissen wie viele Seiten es insgesamt gibt (auÃer wir machen einen Count-Call),
                        // schÃ¤tzen wir oder lassen den User einfach weiterblÃ¤ttern solange Ergebnisse kommen.
                        // Für NBH setzen wir TotalPagesCount fest auf CurrentPage + 1 wenn Ergebnisse kamen:
                        _totalPagesCount = Math.Max(1, (int)Math.Ceiling(total / (double)ItemsPerPage));
                            
                        OnPropertyChanged(nameof(TotalPagesCount));
                        OnPropertyChanged(nameof(PageDisplay));
                    });
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Fehler beim Laden der RPC-Daten.");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private int _totalPagesCount = 1;
        public int TotalPagesCount => _totalPagesCount;

        public async Task ApplyFilter()
        {
            await LoadEntriesFromDatabaseAsync(1);
        }


        private async Task RefreshEntriesAsync()
        {
            IsRefreshing = true;
            try
            {
                await LoadEntriesFromDatabaseAsync();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Fehler beim RefreshEntriesAsync.");
            }
            finally
            {
                IsRefreshing = false;
            }
        }

        private async void OpenDetail(EntryItem entry)
        {
            if (entry == null) return;

            try
            {
                await Shell.Current.GoToAsync(nameof(DetailPage), true, new Dictionary<string, object>
                {
                    ["SelectedEntry"] = entry
                });
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Fehler bei Navigation zur DetailPage");
            }
        }

        private async Task CompleteEntryAsync(EntryItem entry)
        {
            if (entry == null) return;

            try
            {
                entry.is_completed = true;
                var response = await _client.From<EntryItem>().Update(entry);
                if (response != null && response.Models.Any())
                {
                    FilteredEntries.Remove(entry);
                }
                else
                {
                    _logger?.LogWarning("Eintrag konnte nicht auf 'completed' gesetzt werden.");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Fehler beim CompleteEntryAsync");
            }
        }

        private void UpdatePaginationButtons()
        {
            ((Command)NextPageCommand).ChangeCanExecute();
            ((Command)PrevPageCommand).ChangeCanExecute();
            ((Command)FirstPageCommand).ChangeCanExecute();
            ((Command)LastPageCommand).ChangeCanExecute();
        }

        private void RebuildFeaturedEntries()
        {
            FeaturedEntries.Clear();
            foreach (var item in FilteredEntries
                .OrderByDescending(GetFeaturedScore)
                .ThenByDescending(x => x.created_date)
                .Take(5))
            {
                FeaturedEntries.Add(item);
            }

            OnPropertyChanged(nameof(HasFeaturedEntries));
            OnPropertyChanged(nameof(ShowFeaturedSection));
        }

        private static double GetFeaturedScore(EntryItem item)
        {
            if (item == null)
            {
                return 0;
            }

            var score = 0.0;

            // Priorisiere Events mit größerer erwarteter Nachfrage.
            score += Math.Min(20, Math.Max(0, item.max_participants ?? 0) * 2);

            if (string.Equals(item.event_type, "group", StringComparison.OrdinalIgnoreCase))
            {
                score += 8;
            }

            if (item.event_date.HasValue)
            {
                var daysToEvent = (item.event_date.Value.Date - DateTime.UtcNow.Date).TotalDays;
                if (daysToEvent >= 0 && daysToEvent <= 14)
                {
                    score += 5;
                }
            }

            // Recency nur als kleines Tie-Breaking, nicht als Hauptfaktor.
            var ageDays = (DateTime.UtcNow - item.created_date).TotalDays;
            if (ageDays <= 7)
            {
                score += 1;
            }

            return score;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = "")
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}


