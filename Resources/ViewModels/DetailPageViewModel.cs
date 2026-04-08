using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using NBH.Resources.Models;
using NBH.Services;
using Supabase;
using static Supabase.Postgrest.Constants;
using Mopups.Services; // Für das Mopup
using NBH.Resources.Views; // Hier könnte Dein EventRatingMopup liegen
using Newtonsoft.Json;
#if IOS
using NBH.Platforms.iOS;
#endif

namespace NBH.Resources.ViewModels
{
    public partial class DetailPageViewModel : INotifyPropertyChanged
    {
        // ----------------------------------------------------------------
        // FIELDS
        // ----------------------------------------------------------------
        private readonly Supabase.Client _supabaseClient;
        private readonly AuthService _authService;
        private readonly ILogger<DetailPageViewModel> _logger;
        private readonly NotificationService _notificationService; // NEU
        private readonly ModerationService _moderationService;
        private EntryItem? _selectedEntry;
        private string _profileImageUrl = "profile_placeholder.png";
        private string _username = "";
        private string _entryActionButtonText;
        private ICommand _entryActionCommand;
        private bool _isEntryActionButtonVisible;
        private bool _isEventPartnerChoiceVisible;
        private double _finalRating;
        private int _finalRatingCount;
        private bool _isBusy;

        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                if (_isBusy != value)
                {
                    _isBusy = value;
                    OnPropertyChanged();
                }
            }
        }

        // ----------------------------------------------------------------
        // CONSTRUCTOR
        // ----------------------------------------------------------------
        public DetailPageViewModel(
        Supabase.Client supabaseClient,
        AuthService authService,
        ILogger<DetailPageViewModel> logger,
        NotificationService notificationService,
        ModerationService moderationService)
        {
            _supabaseClient = supabaseClient ?? throw new ArgumentNullException(nameof(supabaseClient));
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            _moderationService = moderationService ?? throw new ArgumentNullException(nameof(moderationService));


            // Commands
            GoBackCommand = new Command(async () => await NavigateToAddPageAsync());
            StartChatCommand = new Command(async () => await CreateNewChatAsync());
            NavigateToUserProfileCommand = new Command(async () => await NavigateToUserProfileAsync());
            NavigateToRatingPageCommand = new Command(async () => await NavigateToRatingPageAsync());
            ShowEntryOptionsCommand = new Command(async () => await ShowEntryOptionsAsync());
        }

        // ----------------------------------------------------------------
        // PROPERTIES
        // ----------------------------------------------------------------
        public EntryItem? SelectedEntry
        {
            get => _selectedEntry;
            set
            {
                if (_selectedEntry != value)
                {
                    _selectedEntry = value;
                    OnPropertyChanged();

                    if (_selectedEntry != null)
                    {
                        _logger.LogInformation($"[DetailPageVM] SelectedEntry => Title:{_selectedEntry.title}, Standort:{_selectedEntry.location}");

                        // 1) User laden
                        _ = SafeExecuteAsync(LoadUserDataAsync, nameof(LoadUserDataAsync));

                        // 2) Dynamische Button-Logik
                        _ = SafeExecuteAsync(LoadEntryState, nameof(LoadEntryState));

                        // 3) Gruppen-Event-Daten
                        IsGroupEvent = _selectedEntry.IsGroupEvent;
                        if (IsGroupEvent)
                        {
                            _ = SafeExecuteAsync(
                                () => LoadGroupEventDataAsync(_selectedEntry.id),
                                nameof(LoadGroupEventDataAsync));
                        }
                    }
                }
            }
        }

        public string ProfileImageUrl
        {
            get => _profileImageUrl;
            set
            {
                if (_profileImageUrl != value)
                {
                    _profileImageUrl = value;
                    OnPropertyChanged();
                }
            }
        }

        public string Username
        {
            get => _username;
            set
            {
                if (_username != value)
                {
                    _username = value;
                    OnPropertyChanged();
                }
            }
        }

        public string EntryActionButtonText
        {
            get => _entryActionButtonText;
            set
            {
                if (_entryActionButtonText != value)
                {
                    _entryActionButtonText = value;
                    _logger.LogDebug($"[DEBUG] EntryActionButtonText => {value}");
                    OnPropertyChanged();
                }
            }
        }

        public ICommand EntryActionCommand
        {
            get => _entryActionCommand;
            set
            {
                if (_entryActionCommand != value)
                {
                    _entryActionCommand = value;
                    _logger.LogDebug("[DEBUG] EntryActionCommand gesetzt.");
                    OnPropertyChanged();
                }
            }
        }

        public bool IsEntryActionButtonVisible
        {
            get => _isEntryActionButtonVisible;
            set
            {
                if (_isEntryActionButtonVisible != value)
                {
                    _isEntryActionButtonVisible = value;
                    _logger.LogDebug($"[DEBUG] EntryActionButtonVisible => {value}");
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsSingleActionButtonVisible));
                }
            }
        }

        public bool IsEventPartnerChoiceVisible
        {
            get => _isEventPartnerChoiceVisible;
            set
            {
                if (_isEventPartnerChoiceVisible != value)
                {
                    _isEventPartnerChoiceVisible = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsSingleActionButtonVisible));
                }
            }
        }

        // Nur einen Button anzeigen, wenn IsEntryActionButtonVisible = true
        // und KEIN zusätzlicher Ablehnen-Bereich (IsEventPartnerChoiceVisible=false).
        public bool IsSingleActionButtonVisible => IsEntryActionButtonVisible && !IsEventPartnerChoiceVisible;

        public double FinalRating
        {
            get => _finalRating;
            set
            {
                if (_finalRating != value)
                {
                    _finalRating = value;
                    OnPropertyChanged();
                }
            }
        }

        public int FinalRatingCount
        {
            get => _finalRatingCount;
            set
            {
                if (_finalRatingCount != value)
                {
                    _finalRatingCount = value;
                    OnPropertyChanged();
                }
            }
        }

        // Aktion, um die Sterne visuell zu aktualisieren (z. B. in der View)
        public Action<double, int>? UpdateStarsAction { get; set; }

        // ----------------------------------------------------------------
        // COMMANDS
        // ----------------------------------------------------------------
        public ICommand GoBackCommand { get; }
        public ICommand NavigateToUserProfileCommand { get; }
        public ICommand StartChatCommand { get; }
        public ICommand NavigateToRatingPageCommand { get; }
        public ICommand ShowEntryOptionsCommand { get; }

        public bool IsNotOwner
        {
            get
            {
                if (SelectedEntry == null) return false;
                var currentUserId = _authService.GetCurrentUserId();
                return SelectedEntry.user_id?.ToString() != currentUserId;
            }
        }

        private ICommand _rejectEventCommand;
        public ICommand RejectEventCommand
        {
            get
            {
                return _rejectEventCommand ??= new Command(async () =>
                {
                    _logger.LogInformation(">>> Ablehnen-Button geklickt");
                    var result = await RejectEventAsync();
                    if (result)
                    {
                        _ = SafeExecuteAsync(LoadEntryState, nameof(LoadEntryState));
                    }
                });
            }
        }

        // ----------------------------------------------------------------
        // METHODS (NAVIGATION ETC.)
        // ----------------------------------------------------------------

        private async Task NavigateToAddPageAsync()
        {
            try
            {
                await Shell.Current.GoToAsync("..", true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[NavigateToAddPageAsync] Fehler");
            }
        }


        private async Task NavigateToUserProfileAsync()
        {
            if (SelectedEntry?.user_id == null)
                return;

            try
            {
                _logger.LogInformation($"[NavigateToUserProfileAsync] userId={SelectedEntry.user_id}");

                await Shell.Current.GoToAsync(
                    nameof(UserProfilePage),
                    true,
                    new Dictionary<string, object>
                    {
                        ["userId"] = SelectedEntry.user_id
                    }
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler bei der Navigation zur UserProfilePage");
            }
        }

        private async Task NavigateToRatingPageAsync()
        {
            try
            {
                if (SelectedEntry != null && !string.IsNullOrEmpty(SelectedEntry.user_id))
                {
                    // user_id in URL-Parameter
                    var encodedUserId = Uri.EscapeDataString(SelectedEntry.user_id);

                    // Hier "RatingPage?ReviewedUserId=..."
                    // => Du hast in AppShell: <ShellContent Route="RatingPage" ...>
                    await Shell.Current.GoToAsync($"{nameof(RatingPage)}?ReviewedUserId={encodedUserId}");
                }
                else
                {
                    _logger.LogWarning("[NavigateToRatingPageAsync] => user_id war leer!");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler bei der Navigation zur RatingPage");
            }
        }

        // ----------------------------------------------------------------
        // CHAT ERSTELLEN
        // ----------------------------------------------------------------
        private async Task CreateNewChatAsync()
        {
            if (SelectedEntry == null || string.IsNullOrEmpty(SelectedEntry.user_id))
            {
                _logger.LogWarning("[CreateNewChatAsync] Abbruch => UserId leer");
                return;
            }

            var currentUserId = _authService.GetCurrentUserId();
            if (string.IsNullOrEmpty(currentUserId))
            {
                _logger.LogWarning("[CreateNewChatAsync] currentUserId leer => Abbruch");
                return;
            }

            // NEU: Prüfung, ob man versucht einen Chat mit sich selbst zu erstellen
            if (currentUserId == SelectedEntry.user_id)
            {
#if IOS
                iOSHapticFeedback.TriggerHeavyImpact();
#endif
                HapticFeedback.Perform(HapticFeedbackType.Click);
                await ToastService.ShowInfoAsync(UserFriendlyErrors.Messages.SelfChatWarning);
                _logger.LogWarning("[CreateNewChatAsync] Abbruch => User versucht Chat mit sich selbst zu starten");
                return;
            }

            try
            {
                // Prüfen, ob bereits Chat existiert (user_id==A && partner_id==B ODER user_id==B && partner_id==A)
                if (!Guid.TryParse(SelectedEntry.user_id, out var partnerId))
                {
                    _logger.LogWarning("[CreateNewChatAsync] Partner-ID ist keine gültige Guid.");
                    return;
                }

                var createChatRpcParams = new Dictionary<string, object>
                {
                    { "p_partner_id", partnerId }
                };

                var createChatResponse = await _supabaseClient.Rpc("find_or_create_chat_safe", createChatRpcParams);
                var existingOrNewChatId = string.IsNullOrWhiteSpace(createChatResponse?.Content)
                    ? Guid.Empty
                    : JsonConvert.DeserializeObject<Guid>(createChatResponse.Content);

                if (existingOrNewChatId == Guid.Empty)
                {
                    _logger.LogWarning("[CreateNewChatAsync] Konnte Chat nicht erstellen oder finden.");
                    return;
                }

                await Shell.Current.GoToAsync($"ChatPage?chatId={existingOrNewChatId}");

#if false
                var q1 = await _supabaseClient
                    .From<PublicChat>()
                    .Filter("user_id", Operator.Equals, currentUserId)
                    .Filter("partner_id", Operator.Equals, SelectedEntry.user_id)
                    .Get();

                var q2 = await _supabaseClient
                    .From<PublicChat>()
                    .Filter("user_id", Operator.Equals, SelectedEntry.user_id)
                    .Filter("partner_id", Operator.Equals, currentUserId)
                    .Get();

                var existingChats = q1.Models.Concat(q2.Models).ToList();
                if (existingChats.Any())
                {
                    // Falls Chat vorhanden => direkt öffnen
                    await Shell.Current.GoToAsync($"ChatPage?chatId={existingChats[0].Id}");
                    return;
                }

                // Ansonsten neuen Chat erstellen
                var newChatId = Guid.NewGuid();
                var newChat = new PublicChat
                {
                    Id = newChatId,
                    UserId = currentUserId,
                    PartnerId = SelectedEntry.user_id,
                    LastMessage = "",
                    LastMessageDate = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow
                };

                var insertResp = await _supabaseClient.From<PublicChat>().Insert(newChat);

                if (insertResp != null && insertResp.Models.Any())
                {
                    await Shell.Current.GoToAsync($"ChatPage?chatId={newChatId}");
                }
                else
                {
                    _logger.LogWarning("[CreateNewChatAsync] Konnte neuen Chat nicht erstellen.");
                }
#endif
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CreateNewChatAsync] Fehler");
            }
            finally
            {
                IsBusy = false;
            }
        }


        // ----------------------------------------------------------------
        // NUTZER-DATEN LADEN
        // ----------------------------------------------------------------
        private async Task LoadUserDataAsync()
        {
            if (SelectedEntry?.user_id == null)
                return;

            try
            {
                // Prüfen, ob Session gültig
                if (!await _authService.EnsureSessionValidAsync())
                {
                    _logger.LogWarning("[LoadUserDataAsync] Session ungültig => Abbruch und zurück zum Login");
                    await Shell.Current.GoToAsync("//LoginPage");
                    return;
                }

                var userGuidOk = Guid.TryParse(SelectedEntry.user_id, out var userGuid);
                if (!userGuidOk)
                {
                    _logger.LogWarning("[LoadUserDataAsync] user_id keine gültige GUID => Abbruch");
                    return;
                }

                if (await TryLoadProfileSummaryAsync(userGuid))
                {
                    return;
                }

                var userResponse = await _supabaseClient.Rpc("get_public_user_profile", new Dictionary<string, object>
                {
                    { "p_user_id", userGuid }
                });

                var foundUser = string.IsNullOrWhiteSpace(userResponse?.Content)
                    ? null
                    : JsonConvert.DeserializeObject<PublicUserProfile>(userResponse.Content);

                if (foundUser != null)
                {
                    ProfileImageUrl = string.IsNullOrWhiteSpace(foundUser.ProfileImageUrl)
                        ? "profile_placeholder.png"
                        : _supabaseClient.Storage
                            .From("profile_pictures")
                            .GetPublicUrl(foundUser.ProfileImageUrl);

                    Username = foundUser.Username;

                    if (!string.IsNullOrWhiteSpace(SelectedEntry?.user_id))
                    {
                        await CalculateDetailedRatingAsync(SelectedEntry.user_id);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[LoadUserDataAsync] Fehler");
            }
        }

        private async Task<bool> TryLoadProfileSummaryAsync(Guid userGuid)
        {
            try
            {
                var rpcParams = new Dictionary<string, object>
                {
                    { "p_user_id", userGuid }
                };

                var response = await _supabaseClient.Rpc("get_user_profile_summary", rpcParams);
                if (string.IsNullOrWhiteSpace(response?.Content))
                {
                    return false;
                }

                var summary = JsonConvert.DeserializeObject<UserProfileSummary>(response.Content);
                if (summary?.User == null)
                {
                    return false;
                }

                ProfileImageUrl = string.IsNullOrWhiteSpace(summary.User.ProfileImageUrl)
                    ? "profile_placeholder.png"
                    : _supabaseClient.Storage
                        .From("profile_pictures")
                        .GetPublicUrl(summary.User.ProfileImageUrl);

                Username = summary.User.Username ?? string.Empty;
                FinalRating = Math.Round(summary.Rating?.FinalRating ?? 0.0, 1);
                FinalRatingCount = summary.Rating?.TotalCount ?? 0;

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    UpdateStarsAction?.Invoke(FinalRating, FinalRatingCount);
                });

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[TryLoadProfileSummaryAsync] RPC nicht verfügbar, nutze Fallback.");
                return false;
            }
        }

        // ---------------------------------------------------------
        // Methoden RejectEventAsync (mit Notification)
        // ---------------------------------------------------------
        public async Task<bool> RejectEventAsync()
        {
            if (SelectedEntry == null)
            {
                _logger.LogInformation("RejectEventAsync: SelectedEntry == null => Abbruch");
                return false;
            }

            _logger.LogInformation("RejectEventAsync: event_partner_id = null");

            try
            {
                var updateResp = await _supabaseClient
                    .From<EntryItem>()
                    .Where(e => e.id == SelectedEntry.id)
                    .Set(e => e.event_partner_id, (Guid?)null)
                    .Update();

                if (updateResp.Models == null || updateResp.Models.Count == 0)
                {
                    _logger.LogWarning("RejectEventAsync: Update fehlgeschlagen => Abbruch");
                    return false;
                }

                if (SelectedEntry.event_partner_id.HasValue)
                {
                    var ownerName = _authService.GetCurrentUserEmail();
                    var eventTitle = SelectedEntry.title ?? "Dein Event";
                    var partnerGuid = SelectedEntry.event_partner_id.Value;

                    await _notificationService.AddNotificationAsync(
                        userId: partnerGuid,
                        message: $"{ownerName} hat deine Anfrage für '{eventTitle}' abgelehnt.",
                        type: "event_rejected",
                        actionData: "{ \"hasRatingButton\": false }"
                    );
                }

                _logger.LogInformation("RejectEventAsync: erfolgreich, Notification verschickt.");

                await RefreshEntryStateAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RejectEventAsync: Fehler");
                return false;
            }
            finally
            {
                IsBusy = false;
            }
        }
        // ----------------------------------------------------------------
        // DYNAMISCHE BUTTON-LOGIK LAUFZEIT
        // ----------------------------------------------------------------
        /// <summary>
        /// Neue, vereinfachte Logik:
        ///  - Wenn ich weder Owner noch Partner bin:
        ///       -> Falls kein Partner ? "Annehmen" (klickbar)
        ///       -> Sonst ? kein Button
        ///  - Wenn ich Owner bin:
        ///       -> Kein Partner ? "Warten auf Partner" (nicht klickbar)
        ///       -> Partner da, aber !is_confirmed ? Zwei Buttons: 
        ///             • "Event bestätigen" (klickbar)
        ///             • "Ablehnen" (klickbar)
        ///       -> is_confirmed ? "Bewertung abgeben" (klickbar)
        ///  - Wenn ich Partner bin:
        ///       -> !is_confirmed ? "Warten auf Bestätigung" (nicht klickbar)
        ///       -> is_confirmed ? "Bewertung abgeben" (klickbar)
        /// </summary>
        private async Task LoadEntryState()
        {
            _logger.LogInformation("LoadEntryState() aufgerufen");

            if (SelectedEntry == null)
            {
                _logger.LogInformation("SelectedEntry == null => Button unsichtbar");
                IsEntryActionButtonVisible = false;
                return;
            }

            var currentUserId = _authService.GetCurrentUserId();
            if (string.IsNullOrEmpty(currentUserId))
            {
                IsEntryActionButtonVisible = false;
                return;
            }

            bool amOwner = (SelectedEntry.user_id == currentUserId);
            bool amPartner = false;
            if (SelectedEntry.event_partner_id.HasValue)
            {
                amPartner = (SelectedEntry.event_partner_id.Value.ToString() == currentUserId);
            }

            _logger.LogInformation($"[LoadEntryState] currentUserId={currentUserId}, amOwner={amOwner}, amPartner={amPartner}, is_confirmed={SelectedEntry.is_confirmed}");

            // Standardwert: kein zusätzlicher Bereich für "Ablehnen" anzeigen
            IsEventPartnerChoiceVisible = false;

            // Gruppen-Events nutzen eigene Rollen-/Bewerbungslogik im Contentbereich.
            if (IsGroupEvent)
            {
                IsEntryActionButtonVisible = false;
                return;
            }

            // ------------------
            // Weder Owner noch Partner
            // ------------------
            if (!amOwner && !amPartner)
            {
                if (SelectedEntry.event_partner_id == null)
                {
                    // Kein Partner => "ANNEHMEN"
                    EntryActionButtonText = "ANNEHMEN";
                    EntryActionCommand = new Command(async () =>
                    {
                        _logger.LogInformation(">>> Annehmen-Button geklickt");
                        var ok = await AcceptEntryAsync();
                        if (ok) _ = SafeExecuteAsync(LoadEntryState, nameof(LoadEntryState));
                    });
                    IsEntryActionButtonVisible = true;
                }
                else
                {
                    // Partner existiert, ich bin unbeteiligt => kein Button
                    IsEntryActionButtonVisible = false;
                }
                return;
            }

            // ------------------
            // Ich bin Owner
            // ------------------
            if (amOwner)
            {
                if (SelectedEntry.event_partner_id == null)
                {
                    // Kein Partner => "Warten auf Partner" (nicht klickbar)
                    EntryActionButtonText = "WARTEN AUF PARTNER";
                    EntryActionCommand = new Command(() => { /* no-op */ });
                    IsEntryActionButtonVisible = true;
                    return;
                }
                else
                {
                    // Partner existiert
                    if (!SelectedEntry.is_confirmed)
                    {
                        // Noch nicht bestätigt => 2 Buttons: Bestätigen + Ablehnen
                        EntryActionButtonText = "EVENT BESTÄTIGEN";
                        EntryActionCommand = new Command(async () =>
                        {
                            _logger.LogInformation(">>> Event bestätigen geklickt");
                            var ok = await ConfirmEntryAsync();
                            if (ok) _ = SafeExecuteAsync(LoadEntryState, nameof(LoadEntryState));
                        });
                        IsEventPartnerChoiceVisible = true;
                        IsEntryActionButtonVisible = true;
                        return;
                    }
                    else
                    {
                        // is_confirmed => "Bewertung abgeben"
                        EntryActionButtonText = "BEWERTUNG ABGEBEN";
                        EntryActionCommand = new Command(async () =>
                        {
                            _logger.LogInformation(">>> Bewertung abgeben (Owner) geklickt");
                            await ShowEventRatingMopupAsync();
                            // Button ausblenden (nach dem Bewerten?)
                            IsEntryActionButtonVisible = false;
                        });
                        IsEntryActionButtonVisible = true;
                        return;
                    }
                }
            }

            // ------------------
            // Ich bin Partner
            // ------------------
            if (amPartner)
            {
                if (!SelectedEntry.is_confirmed)
                {
                    // "Warten auf Bestätigung"
                    EntryActionButtonText = "WARTEN AUF BESTÄTIGUNG";
                    EntryActionCommand = new Command(() => { /* no-op */ });
                    IsEntryActionButtonVisible = true;
                    return;
                }
                else
                {
                    // is_confirmed => "Bewertung abgeben"
                    EntryActionButtonText = "BEWERTUNG ABGEBEN";
                    EntryActionCommand = new Command(async () =>
                    {
                        _logger.LogInformation(">>> Bewertung abgeben (Partner) geklickt");
                        await ShowEventRatingMopupAsync();
                        // Button ausblenden
                        IsEntryActionButtonVisible = false;
                    });
                    IsEntryActionButtonVisible = true;
                }
            }
        }

        // ----------------------------------------------------------------
        // EINTRAG STATE NEU LADEN
        // ----------------------------------------------------------------
        public async Task RefreshEntryStateAsync()
        {
            if (SelectedEntry == null)
            {
                _logger.LogInformation("RefreshEntryStateAsync: Kein SelectedEntry => Abbruch");
                return;
            }

            try
            {
                _logger.LogInformation("RefreshEntryStateAsync: Hole aktuellen Eintrag via supabase...");

                var getResp = await _supabaseClient
                    .From<EntryItem>()
                    .Where(e => e.id == SelectedEntry.id)
                    .Get();

                var updatedEntry = getResp.Models.FirstOrDefault();
                if (updatedEntry == null)
                {
                    _logger.LogInformation("RefreshEntryStateAsync: Konnte kein aktualisiertes Entry finden => Abbruch");
                    return;
                }

                _logger.LogInformation("RefreshEntryStateAsync: Aktualisiere SelectedEntry und rufe LoadEntryState() auf");
                SelectedEntry = updatedEntry;

                _ = SafeExecuteAsync(LoadEntryState, nameof(LoadEntryState));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RefreshEntryStateAsync: Exception");
            }
        }

        // ---------------------------------------------------------
        // Methoden AcceptEntryAsync (mit Notification)
        // ---------------------------------------------------------
        public async Task<bool> AcceptEntryAsync()
        {
            if (SelectedEntry == null)
            {
                _logger.LogInformation("AcceptEntryAsync: SelectedEntry == null => Abbruch");
                return false;
            }

            _logger.LogInformation("AcceptEntryAsync: event_partner_id = currentUserId");

            var currentUserId = _authService.GetCurrentUserId();
            if (!Guid.TryParse(currentUserId, out _))
            {
                _logger.LogWarning("AcceptEntryAsync: currentUserId kein gültiger GUID => Abbruch");
                return false;
            }

            try
            {
                var acceptRpcParams = new Dictionary<string, object>
                {
                    { "p_entry_id", SelectedEntry.id }
                };

                var acceptResponse = await _supabaseClient.Rpc("accept_entry_partner_safe", acceptRpcParams);
                var ownerGuidFromRpc = string.IsNullOrWhiteSpace(acceptResponse?.Content)
                    ? Guid.Empty
                    : JsonConvert.DeserializeObject<Guid>(acceptResponse.Content);

                if (ownerGuidFromRpc == Guid.Empty)
                {
                    _logger.LogWarning("AcceptEntryAsync: RPC lieferte keinen Owner zurück => Abbruch");
                    return false;
                }

                var partnerDisplayName = _authService.GetCurrentUserEmail();
                var acceptedEventTitle = SelectedEntry.title ?? "Dein Event";

                await _notificationService.AddNotificationAsync(
                    userId: ownerGuidFromRpc,
                    message: $"{partnerDisplayName} hat dein Event '{acceptedEventTitle}' angenommen.",
                    type: "event_accepted",
                    actionData: $"{{ \"event_id\":\"{SelectedEntry.id}\", \"HasRatingButton\":true }}"
                );

                _logger.LogInformation("AcceptEntryAsync: erfolgreich, Notification verschickt.");

                await RefreshEntryStateAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AcceptEntryAsync: Fehler");
                return false;
            }
            finally
            {
                IsBusy = false;
            }
        }


        // ---------------------------------------------------------
        // Methode ConfirmEntryAsync (mit Notification)
        // ---------------------------------------------------------
        public async Task<bool> ConfirmEntryAsync()
        {
            if (SelectedEntry == null)
            {
                _logger.LogInformation("ConfirmEntryAsync: SelectedEntry == null => Abbruch");
                return false;
            }

            _logger.LogInformation("ConfirmEntryAsync: is_confirmed=true, auto_complete_at=+48h");

            IsBusy = true;
            try
            {
                var updatedEntry = await _supabaseClient
                    .From<EntryItem>()
                    .Where(e => e.id == SelectedEntry.id)
                    .Set(e => e.is_confirmed, true)
                    .Set(e => e.auto_complete_at, DateTime.UtcNow.AddHours(48))
                    .Update();

                if (updatedEntry.Models == null || updatedEntry.Models.Count == 0)
                {
                    _logger.LogWarning("ConfirmEntryAsync: Update fehlgeschlagen => Abbruch");
                    return false;
                }

                // --- NEU: Notification an Partner (Bewerten möglich!) ---
                if (SelectedEntry.event_partner_id.HasValue)
                {
                    var ownerName = _authService.GetCurrentUserEmail();
                    var eventTitle = SelectedEntry.title ?? "Dein Event";
                    var partnerGuid = SelectedEntry.event_partner_id.Value;

                    await _notificationService.AddNotificationAsync(
                    userId: partnerGuid,
                    message: $"{ownerName} hat '{eventTitle}' bestätigt. Ihr könnt jetzt bewerten!",
                    type: "event_confirmed",
                    actionData: $"{{ \"event_id\": \"{SelectedEntry.id}\", \"HasRatingButton\": true }}"

                    );
                }

                _logger.LogInformation("ConfirmEntryAsync: erfolgreich, Notification verschickt.");

                await RefreshEntryStateAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ConfirmEntryAsync: Fehler");
                return false;
            }
        }

        // ----------------------------------------------------------------
        // PRÜFEN, OB BEWERTUNG SCHON VORHANDEN
        // ----------------------------------------------------------------
        public async Task<bool> CheckIfReviewAllowed()
        {
            if (SelectedEntry == null)
                return false;

            if (string.IsNullOrEmpty(_authService.GetCurrentUserId()))
                return false;

            try
            {
                var rpcParams = new Dictionary<string, object>
                {
                    { "p_event_id", SelectedEntry.id }
                };

                var response = await _supabaseClient.Rpc("can_submit_event_review_safe", rpcParams);
                return !string.IsNullOrWhiteSpace(response?.Content) &&
                       JsonConvert.DeserializeObject<bool>(response.Content);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CheckIfReviewAllowed] Fehler");
                return false;
            }
        }

        // ----------------------------------------------------------------
        // BEWERTUNGS-MODAL ÖFFNEN
        // ----------------------------------------------------------------
        private async Task ShowEventRatingMopupAsync()
        {
            if (SelectedEntry == null)
                return;

            var currentUserId = _authService.GetCurrentUserId();
            if (!Guid.TryParse(currentUserId, out var reviewerGuid))
            {
                _logger.LogWarning("[ShowEventRatingMopupAsync] reviewerGuid kann nicht geparst werden.");
                return;
            }

            if (!Guid.TryParse(SelectedEntry.user_id, out var reviewedGuid))
            {
                _logger.LogWarning("[ShowEventRatingMopupAsync] reviewedId kann nicht geparst werden.");
                return;
            }

            try
            {
                // Mopup öffnen
                await MopupService.Instance.PushAsync(new EventRatingMopup(
                    eventId: SelectedEntry.id,
                    reviewerId: reviewerGuid,
                    reviewedId: reviewedGuid,
                    authService: _authService
                ));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Öffnen des EventRatingMopup");
            }
        }

        // ----------------------------------------------------------------
        // GESAMT-RATING BERECHNEN (OPTIMIZED VIA RPC)
        // ----------------------------------------------------------------
        private async Task CalculateDetailedRatingAsync(string userIdStr)
        {
            try
            {
                if (!Guid.TryParse(userIdStr, out var userGuid))
                {
                    _logger.LogWarning("[CalculateDetailedRatingAsync] userId ist keine gültige GUID");
                    return;
                }

                var rpcParams = new Dictionary<string, object> { { "p_user_id", userGuid } };
                var response = await _supabaseClient.Rpc("get_user_rating_stats", rpcParams);

                if (response != null && response.Content != null)
                {
                    var stats = JsonConvert.DeserializeObject<RatingStats>(response.Content);
                    if (stats != null)
                    {
                        FinalRating = Math.Round(stats.FinalRating, 1);
                        FinalRatingCount = stats.TotalCount;
                        _logger.LogInformation($"[CalculateDetailedRatingAsync] RPC => finalVal={FinalRating:F1}, totalCount={FinalRatingCount}");

                        // Sterne in der View aktualisieren
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            UpdateStarsAction?.Invoke(FinalRating, FinalRatingCount);
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CalculateDetailedRatingAsync] Fehler via RPC");
            }
        }

        public async Task LoadEntryByIdAsync(Guid entryId)
        {
            try
            {
                var response = await _supabaseClient
                    .From<EntryItem>()
                    .Where(e => e.id == entryId)
                    .Get();

                SelectedEntry = response.Models.FirstOrDefault();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Laden des Entries via EntryId");
            }
        }


        // ----------------------------------------------------------------
        // INotifyPropertyChanged
        // ----------------------------------------------------------------
        // ----------------------------------------------------------------
        // Eintrag melden
        // ----------------------------------------------------------------
        private async Task ShowEntryOptionsAsync()
        {
            if (SelectedEntry == null) return;

            var action = await Shell.Current.DisplayActionSheetAsync(
                "Optionen", "Abbrechen", null,
                "Eintrag melden");

            if (action == "Eintrag melden")
            {
                var reason = await Shell.Current.DisplayActionSheetAsync(
                    "Grund wählen", "Abbrechen", null,
                    "Spam", "Unangemessener Inhalt", "Falsche Informationen", "Anderes");

                if (reason != null && reason != "Abbrechen")
                {
                    var entryId = SelectedEntry.id;
                    var success = await _moderationService.ReportEntryAsync(entryId, reason, null);
                    if (success)
                        await ToastService.ShowSuccessAsync(UserFriendlyErrors.Messages.ReportSent);
                    else
                        await ToastService.ShowErrorAsync(UserFriendlyErrors.Messages.ReportError);
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = "")
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        private async Task SafeExecuteAsync(Func<Task> action, string operationName)
        {
            try
            {
                await action();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[DetailPageViewModel] Fehler in {OperationName}", operationName);
            }
        }
    }
}
