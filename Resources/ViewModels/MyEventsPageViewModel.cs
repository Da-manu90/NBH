using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using NBH.Resources.Models;
using NBH.Services;
using static Supabase.Postgrest.Constants;

namespace NBH.Resources.ViewModels
{
    public partial class MyEventsPageViewModel : BaseViewModel
    {
        private readonly Supabase.Client _client;
        private readonly AuthService _authService;
        private readonly ILogger<MyEventsPageViewModel> _logger;

        private Guid _currentUserId;
        private readonly HashSet<Guid> _hiddenEntryIds = new();

        public ObservableCollection<EntryItem> CreatedEvents { get; } = new();
        public ObservableCollection<EntryItem> ParticipatingEvents { get; } = new();
        public ObservableCollection<EntryItem> VisibleEvents { get; } = new();

        private string _selectedSection = "created";
        public string SelectedSection
        {
            get => _selectedSection;
            set
            {
                if (_selectedSection == value)
                {
                    return;
                }

                _selectedSection = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsCreatedSection));
                OnPropertyChanged(nameof(IsParticipatingSection));
                RebuildVisibleEvents();
            }
        }

        public bool IsCreatedSection => SelectedSection == "created";
        public bool IsParticipatingSection => SelectedSection == "participating";
        public bool HasVisibleEvents => VisibleEvents.Count > 0;

        public ICommand ShowCreatedCommand { get; }
        public ICommand ShowParticipatingCommand { get; }
        public ICommand OpenDetailCommand { get; }
        public ICommand OpenOptionsCommand { get; }

        public MyEventsPageViewModel(
            Supabase.Client client,
            AuthService authService,
            ILogger<MyEventsPageViewModel> logger)
        {
            _client = client;
            _authService = authService;
            _logger = logger;

            ShowCreatedCommand = new Command(() => SelectedSection = "created");
            ShowParticipatingCommand = new Command(() => SelectedSection = "participating");
            OpenDetailCommand = new Command<EntryItem>(async entry => await OpenDetailAsync(entry));
            OpenOptionsCommand = new Command<EntryItem>(async entry => await OpenOptionsAsync(entry));
        }

        public async Task InitializeAsync(string? initialSection)
        {
            SelectedSection = string.Equals(initialSection, "participating", StringComparison.OrdinalIgnoreCase)
                ? "participating"
                : "created";

            if (!Guid.TryParse(_authService.GetCurrentUserId(), out _currentUserId))
            {
                return;
            }

            await LoadHiddenEntriesAsync();
            await LoadCreatedEventsAsync();
            await LoadParticipatingEventsAsync();
            RebuildVisibleEvents();
        }

        private async Task LoadHiddenEntriesAsync()
        {
            try
            {
                var response = await _client.From<UserHiddenEntry>()
                    .Where(x => x.UserId == _currentUserId)
                    .Get();

                _hiddenEntryIds.Clear();
                foreach (var item in response.Models)
                {
                    _hiddenEntryIds.Add(item.EntryId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Hidden entries konnten nicht geladen werden.");
            }
        }

        private async Task LoadCreatedEventsAsync()
        {
            try
            {
                var userIdText = _currentUserId.ToString();
                var created = await _client.From<EntryItem>()
                    .Filter("user_id", Operator.Equals, userIdText)
                    .Order(x => x.created_date, Supabase.Postgrest.Constants.Ordering.Descending)
                    .Get();

                CreatedEvents.Clear();
                foreach (var entry in created.Models.Where(x => !_hiddenEntryIds.Contains(x.id)))
                {
                    CreatedEvents.Add(entry);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Eigene Events konnten nicht geladen werden.");
            }
        }

        private async Task LoadParticipatingEventsAsync()
        {
            try
            {
                var result = new List<EntryItem>();
                var userIdText = _currentUserId.ToString();

                // Single events where user is accepted partner.
                var singlePartnerEntries = await _client.From<EntryItem>()
                    .Filter("event_partner_id", Operator.Equals, userIdText)
                    .Filter("is_completed", Operator.Equals, "false")
                    .Order(x => x.created_date, Supabase.Postgrest.Constants.Ordering.Descending)
                    .Get();

                result.AddRange(singlePartnerEntries.Models);

                // Group events where user is pending/confirmed participant.
                var participantRows = await _client.From<GroupEventParticipant>()
                    .Filter("user_id", Operator.Equals, userIdText)
                    .Get();

                var relevantEntryIds = participantRows.Models
                    .Where(x => x.Status == "pending" || x.Status == "confirmed")
                    .Select(x => x.EntryId)
                    .Distinct()
                    .ToList();

                foreach (var entryId in relevantEntryIds)
                {
                    var entryResponse = await _client.From<EntryItem>()
                        .Filter("id", Operator.Equals, entryId.ToString())
                        .Get();

                    var entry = entryResponse.Models.FirstOrDefault();
                    if (entry != null)
                    {
                        result.Add(entry);
                    }
                }

                ParticipatingEvents.Clear();
                foreach (var entry in result
                    .Where(x => !_hiddenEntryIds.Contains(x.id))
                    .GroupBy(x => x.id)
                    .Select(g => g.First())
                    .OrderByDescending(x => x.created_date))
                {
                    ParticipatingEvents.Add(entry);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Teilnahme-Events konnten nicht geladen werden.");
            }
        }

        private void RebuildVisibleEvents()
        {
            VisibleEvents.Clear();
            var source = IsCreatedSection ? CreatedEvents : ParticipatingEvents;
            foreach (var entry in source)
            {
                VisibleEvents.Add(entry);
            }

            OnPropertyChanged(nameof(HasVisibleEvents));
        }

        private async Task OpenDetailAsync(EntryItem? entry)
        {
            if (entry == null)
            {
                return;
            }

            await Shell.Current.GoToAsync(nameof(DetailPage), true, new Dictionary<string, object>
            {
                ["SelectedEntry"] = entry
            });
        }

        private async Task OpenOptionsAsync(EntryItem? entry)
        {
            if (entry == null)
            {
                return;
            }

            var action = await Shell.Current.DisplayActionSheet(
                "Event Optionen",
                "Abbrechen",
                null,
                "Bearbeiten",
                "Löschen");

            if (action == "Bearbeiten")
            {
                await Shell.Current.GoToAsync($"{nameof(EditEventPage)}?entryId={entry.id}");
                return;
            }

            if (action == "Löschen")
            {
                await HideEventForCurrentUserAsync(entry);
            }
        }

        private async Task HideEventForCurrentUserAsync(EntryItem entry)
        {
            try
            {
                await _client.From<UserHiddenEntry>().Insert(new UserHiddenEntry
                {
                    UserId = _currentUserId,
                    EntryId = entry.id,
                    HiddenAt = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Event wurde evtl. bereits verborgen.");
            }

            _hiddenEntryIds.Add(entry.id);
            CreatedEvents.Remove(entry);
            ParticipatingEvents.Remove(entry);
            RebuildVisibleEvents();
        }
    }
}
