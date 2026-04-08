using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;
using NBH.Resources.Models;
using NBH.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace NBH.Resources.ViewModels
{
    public partial class RoleInputModel : ObservableObject
    {
        [ObservableProperty]
        private string _roleName = string.Empty;

        [ObservableProperty]
        private int _maxSlots = 1;
    }

    public partial class CreateEventPageViewModel : ObservableObject, IRecipient<EventImageUploadedMessage>
    {
        private readonly Supabase.Client _client;
        private readonly ILogger<CreateEventPageViewModel> _logger;

        [ObservableProperty]
        private bool _isBusy;

        public CreateEventPageViewModel(Supabase.Client client, ILogger<CreateEventPageViewModel> logger)
        {
            _client = client;
            _logger = logger;
            
            // Set initial state
            EventType = "single";
            SetEventTypeCommand.Execute("single");

            WeakReferenceMessenger.Default.Register(this);
        }

        [ObservableProperty]
        private string _title = string.Empty;

        [ObservableProperty]
        private string _description = string.Empty;

        [ObservableProperty]
        private string _category = "Anfrage";

        [ObservableProperty]
        private string _location = string.Empty;

        // Note: Image capture integration can be added later as requested.
        [ObservableProperty]
        private string? _imageSource;

        [ObservableProperty]
        private DateTime? _startDate = DateTime.Today;

        [ObservableProperty]
        private TimeSpan? _startTime = DateTime.Now.TimeOfDay;

        [ObservableProperty]
        private DateTime? _endDate = DateTime.Today;

        [ObservableProperty]
        private TimeSpan? _endTime = DateTime.Now.TimeOfDay.Add(TimeSpan.FromHours(2));

        public void Receive(EventImageUploadedMessage message)
        {
            ImageSource = message.Value;
        }

        [RelayCommand]
        private async Task SelectImageAsync()
        {
            try
            {
                var file = await FilePicker.PickAsync(new PickOptions
                {
                    FileTypes = FilePickerFileType.Images,
                    PickerTitle = "WÃ¤hle ein Event-Bild"
                });

                if (file == null) return;

                using var originalStream = await file.OpenReadAsync();
                using var image = await SixLabors.ImageSharp.Image.LoadAsync(originalStream);

                if (image == null)
                {
                    await ToastService.ShowErrorAsync(UserFriendlyErrors.Messages.InvalidImage);
                    return;
                }

                image.Mutate(ctx => ctx.Resize(new ResizeOptions
                {
                    Mode = SixLabors.ImageSharp.Processing.ResizeMode.Max,
                    Size = new SixLabors.ImageSharp.Size(800, 800)
                }));

                var outStream = new MemoryStream();
                await image.SaveAsPngAsync(outStream);
                outStream.Position = 0;

                NavigationService.CropImageStream = outStream;
                NavigationService.ImageCropMode = "Event";
                
                await Shell.Current.GoToAsync(Routes.CropImage);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Fehler bei SelectImageAsync");
                await ToastService.ShowErrorAsync(UserFriendlyErrors.Messages.ImageProcessingError);
            }
        }

        // StartDate/StartTime und EndDate/EndTime ersetzen die alten EventDate/EventEndDate Properties

        [ObservableProperty]
        private string _eventType = "single";

        [ObservableProperty]
        private int? _maxParticipants;

        [ObservableProperty]
        private ObservableCollection<RoleInputModel> _roles = new();

        public bool IsGroupEvent => EventType == "group";

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
            if (role != null && Roles.Contains(role))
            {
                Roles.Remove(role);
            }
        }

        [RelayCommand]
        private async Task CreateEventAsync()
        {
            System.Diagnostics.Debug.WriteLine("=== CreateEventAsync START ===");

            if (IsBusy)
                return;

            if (string.IsNullOrWhiteSpace(Title) || string.IsNullOrWhiteSpace(Location))
            {
                await ToastService.ShowWarningAsync(UserFriendlyErrors.Messages.FillTitleAndLocation);
                return;
            }

            var normalizedRoles = Roles
                .Select(r => new RoleInputModel
                {
                    RoleName = (r.RoleName ?? string.Empty).Trim(),
                    MaxSlots = r.MaxSlots <= 0 ? 1 : r.MaxSlots
                })
                .Where(r => !string.IsNullOrWhiteSpace(r.RoleName))
                .ToList();

            if (IsGroupEvent)
            {
                if (normalizedRoles.Count == 0 && (MaxParticipants == null || MaxParticipants <= 0))
                {
                    await ToastService.ShowWarningAsync(UserFriendlyErrors.Messages.FillParticipants);
                    return;
                }

                if (Roles.Any(r => string.IsNullOrWhiteSpace(r.RoleName) && r.MaxSlots > 0))
                {
                    await ToastService.ShowWarningAsync(UserFriendlyErrors.Messages.FillRoleNames);
                    return;
                }
            }

            // Datum + Uhrzeit kombinieren
            DateTime? combinedStart = (StartDate.HasValue && StartTime.HasValue)
                ? StartDate.Value.Date + StartTime.Value
                : null;

            DateTime? combinedEnd = (EndDate.HasValue && EndTime.HasValue)
                ? EndDate.Value.Date + EndTime.Value
                : null;

            var userId = _client?.Auth.CurrentUser?.Id;

            // ✅ WICHTIG: echtes Array, KEIN String!
            var rolesArray = (IsGroupEvent && normalizedRoles.Any())
                ? normalizedRoles.Select(r => new Dictionary<string, object>
                {
        { "role_name", r.RoleName.Trim() },
        { "max_slots", r.MaxSlots }
                }).ToList()
                : new List<Dictionary<string, object>>();

            var rpcParams = new Dictionary<string, object>
    {
        { "p_title", Title },
        { "p_description", Description ?? "" },
        { "p_category", Category ?? "Allgemein" },
        { "p_location", Location },
        { "p_user_id", userId },
        { "p_event_type", EventType ?? "single" },
        { "p_max_participants", IsGroupEvent && MaxParticipants.HasValue ? MaxParticipants.Value : 0 },
        { "p_event_image_url", ImageSource ?? "" },

        // 🔥 FIX: KEIN SerializeObject!
        { "p_roles_json", rolesArray }
    };

            // Datum nur senden wenn vorhanden
            rpcParams["p_event_date"] = combinedStart?.ToUniversalTime().ToString("o") ?? "";
            rpcParams["p_event_end_date"] = combinedEnd?.ToUniversalTime().ToString("o") ?? "";

            System.Diagnostics.Debug.WriteLine(
                $"[CreateEvent] RPC params: {Newtonsoft.Json.JsonConvert.SerializeObject(rpcParams)}");

            IsBusy = true;
            try
            {
                var response = await _client.Rpc("create_event_with_roles", rpcParams);

                System.Diagnostics.Debug.WriteLine($"[CreateEvent] RPC SUCCESS: {response?.Content}");

                await ToastService.ShowSuccessAsync(UserFriendlyErrors.Messages.EventCreated);
                await Shell.Current.GoToAsync("..");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CreateEvent] ERROR: {ex.Message}");
                _logger?.LogError(ex, "Fehler beim Erstellen des Events");

                await ToastService.ShowErrorAsync(UserFriendlyErrors.GetMessage(ex));
            }
            finally
            {
                IsBusy = false;
            }

            System.Diagnostics.Debug.WriteLine("=== CreateEventAsync END ===");
        }
    }
}
