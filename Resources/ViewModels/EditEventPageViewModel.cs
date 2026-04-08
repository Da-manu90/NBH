using System;
using System.Linq;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using NBH.Resources.Models;
using NBH.Services;

namespace NBH.Resources.ViewModels
{
    public partial class EditEventPageViewModel : ObservableObject
    {
        private readonly Supabase.Client _client;
        private readonly ILogger<EditEventPageViewModel> _logger;

        private Guid _entryId = Guid.Empty;

        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private string _title = string.Empty;

        [ObservableProperty]
        private string _description = string.Empty;

        [ObservableProperty]
        private string _category = "Anfrage";

        [ObservableProperty]
        private string _location = string.Empty;

        [ObservableProperty]
        private string? _imageSource;

        [ObservableProperty]
        private DateTime? _startDate = DateTime.Today;

        [ObservableProperty]
        private TimeSpan? _startTime = DateTime.Now.TimeOfDay;

        [ObservableProperty]
        private DateTime? _endDate = DateTime.Today;

        [ObservableProperty]
        private TimeSpan? _endTime = DateTime.Now.TimeOfDay.Add(TimeSpan.FromHours(1));

        [ObservableProperty]
        private string _eventType = "single";

        [ObservableProperty]
        private int? _maxParticipants;

        [ObservableProperty]
        private ObservableCollection<RoleInputModel> _roles = new();

        public bool IsGroupEvent => EventType == "group";

        public EditEventPageViewModel(Supabase.Client client, ILogger<EditEventPageViewModel> logger)
        {
            _client = client;
            _logger = logger;
        }

        public async Task InitializeAsync(Guid entryId)
        {
            if (entryId == Guid.Empty)
            {
                return;
            }

            _entryId = entryId;
            await LoadEntryAsync();
        }

        [RelayCommand]
        private void SetEventType(string type)
        {
            EventType = type;
            OnPropertyChanged(nameof(IsGroupEvent));
        }

        [RelayCommand]
        private void AddRole()
        {
            Roles.Add(new RoleInputModel());
        }

        [RelayCommand]
        private void RemoveRole(RoleInputModel role)
        {
            if (Roles.Contains(role))
            {
                Roles.Remove(role);
            }
        }

        [RelayCommand]
        private async Task SaveAsync()
        {
            if (_entryId == Guid.Empty || string.IsNullOrWhiteSpace(Title) || string.IsNullOrWhiteSpace(Location))
            {
                await ToastService.ShowWarningAsync("Bitte Titel und Ort ausfüllen.");
                return;
            }

            IsBusy = true;
            try
            {
                DateTime? combinedStart = (StartDate.HasValue && StartTime.HasValue)
                    ? StartDate.Value.Date + StartTime.Value
                    : null;

                DateTime? combinedEnd = (EndDate.HasValue && EndTime.HasValue)
                    ? EndDate.Value.Date + EndTime.Value
                    : null;

                await _client.From<EntryItem>()
                    .Where(x => x.id == _entryId)
                    .Set(x => x.title, Title.Trim())
                    .Set(x => x.description, Description?.Trim() ?? string.Empty)
                    .Set(x => x.category, Category)
                    .Set(x => x.location, Location.Trim())
                    .Set(x => x.event_type, EventType)
                    .Set(x => x.max_participants, IsGroupEvent ? MaxParticipants : null)
                    .Set(x => x.event_date, combinedStart?.ToUniversalTime())
                    .Set(x => x.event_end_date, combinedEnd?.ToUniversalTime())
                    .Update();

                if (IsGroupEvent)
                {
                    await _client.From<GroupEventRole>()
                        .Where(r => r.EntryId == _entryId)
                        .Delete();

                    var normalized = Roles
                        .Where(r => !string.IsNullOrWhiteSpace(r.RoleName))
                        .Select(r => new GroupEventRole
                        {
                            Id = Guid.NewGuid(),
                            EntryId = _entryId,
                            RoleName = r.RoleName.Trim(),
                            MaxSlots = r.MaxSlots <= 0 ? 1 : r.MaxSlots,
                            CreatedAt = DateTime.UtcNow
                        })
                        .ToList();

                    if (normalized.Count > 0)
                    {
                        await _client.From<GroupEventRole>().Insert(normalized);
                    }
                }

                await ToastService.ShowSuccessAsync("Event wurde gespeichert.");
                await Shell.Current.GoToAsync("..");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Speichern des Events.");
                await ToastService.ShowErrorAsync(UserFriendlyErrors.Messages.EventLoadError);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task LoadEntryAsync()
        {
            IsBusy = true;
            try
            {
                var response = await _client.From<EntryItem>()
                    .Where(x => x.id == _entryId)
                    .Get();

                var entry = response.Models.FirstOrDefault();
                if (entry == null)
                {
                    return;
                }

                Title = entry.title ?? string.Empty;
                Description = entry.description ?? string.Empty;
                Category = string.IsNullOrWhiteSpace(entry.category) ? "Anfrage" : entry.category;
                Location = entry.location ?? string.Empty;
                EventType = string.IsNullOrWhiteSpace(entry.event_type) ? "single" : entry.event_type;
                MaxParticipants = entry.max_participants;
                ImageSource = entry.event_image_url;
                StartDate = entry.event_date?.ToLocalTime().Date ?? DateTime.Today;
                StartTime = entry.event_date?.ToLocalTime().TimeOfDay ?? DateTime.Now.TimeOfDay;
                EndDate = entry.event_end_date?.ToLocalTime().Date ?? DateTime.Today;
                EndTime = entry.event_end_date?.ToLocalTime().TimeOfDay ?? DateTime.Now.TimeOfDay.Add(TimeSpan.FromHours(1));

                Roles.Clear();
                if (EventType == "group")
                {
                    var rolesResponse = await _client.From<GroupEventRole>()
                        .Where(r => r.EntryId == _entryId)
                        .Get();

                    foreach (var role in rolesResponse.Models)
                    {
                        Roles.Add(new RoleInputModel
                        {
                            RoleName = role.RoleName,
                            MaxSlots = role.MaxSlots
                        });
                    }
                }

                OnPropertyChanged(nameof(IsGroupEvent));
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
