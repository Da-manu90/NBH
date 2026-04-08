using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls;
using NBH.Services;
using Supabase;
using NBH.Resources.Models;
using Newtonsoft.Json;

namespace NBH.Resources.ViewModels
{
    public partial class DetailPageViewModel
    {
        // ==========================================
        // PROPERTYS FÜR GRUPPEN-EVENTS
        // ==========================================

        private List<GroupEventRole> _eventRoles = new();
        public List<GroupEventRole> EventRoles
        {
            get => _eventRoles;
            set { _eventRoles = value; OnPropertyChanged(); }
        }

        private List<GroupEventParticipant> _confirmedParticipants = new();
        public List<GroupEventParticipant> ConfirmedParticipants
        {
            get => _confirmedParticipants;
            set { _confirmedParticipants = value; OnPropertyChanged(); }
        }

        private List<GroupEventParticipant> _pendingApplicants = new();
        public List<GroupEventParticipant> PendingApplicants
        {
            get => _pendingApplicants;
            set { _pendingApplicants = value; OnPropertyChanged(); }
        }

        public int TotalConfirmedCount => ConfirmedParticipants?.Count ?? 0;
        public bool HasConfirmedParticipants => ConfirmedParticipants?.Count > 0;
        
        public double GeneralFillPercentage => SelectedEntry?.max_participants > 0 
            ? (double)TotalConfirmedCount / SelectedEntry.max_participants.Value * 100.0 
            : 0;

        private bool _isGroupEvent;
        public bool IsGroupEvent
        {
            get => _isGroupEvent;
            set { _isGroupEvent = value; OnPropertyChanged(); }
        }

        private bool _isCurrentUserParticipant;
        public bool IsCurrentUserParticipant
        {
            get => _isCurrentUserParticipant;
            set { _isCurrentUserParticipant = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanApply)); }
        }

        private string _currentUserStatus = string.Empty;
        public string CurrentUserStatus
        {
            get => _currentUserStatus;
            set { _currentUserStatus = value; OnPropertyChanged(); }
        }

        public bool IsOwner => SelectedEntry?.user_id == _authService.GetCurrentUserId();
        
        public bool CanApply => !IsCurrentUserParticipant && IsGroupEvent && !IsOwner;
        public bool CanWithdrawApplication => IsCurrentUserParticipant && CurrentUserStatus == "pending" && !IsOwner;
        public bool HasPendingApplicants => PendingApplicants?.Count > 0;
        public string CurrentUserStatusText => string.IsNullOrWhiteSpace(CurrentUserStatus)
            ? "Keine Bewerbung"
            : CurrentUserStatus switch
            {
                "pending" => "Bewerbung ausstehend",
                "confirmed" => "Bewerbung bestaetigt",
                "rejected" => "Bewerbung abgelehnt",
                _ => CurrentUserStatus
            };

        // ==========================================
        // COMMANDS & METHODEN
        // ==========================================

        private ICommand? _applyAsParticipantCommand;
        public ICommand ApplyAsParticipantCommand => _applyAsParticipantCommand ??= new Command<Guid?>(async (roleId) => await ApplyAsParticipantAsync(roleId));

        private ICommand? _confirmApplicantCommand;
        public ICommand ConfirmApplicantCommand => _confirmApplicantCommand ??= new Command<Guid>(async (participantId) => await ConfirmApplicantAsync(participantId));

        private ICommand? _rejectApplicantCommand;
        public ICommand RejectApplicantCommand => _rejectApplicantCommand ??= new Command<Guid>(async (participantId) => await RejectApplicantAsync(participantId));

        private ICommand? _withdrawApplicationCommand;
        public ICommand WithdrawApplicationCommand => _withdrawApplicationCommand ??= new Command(async () => await WithdrawApplicationAsync());

        private ICommand? _openGroupConversationCommand;
        public ICommand OpenGroupConversationCommand => _openGroupConversationCommand ??= new Command(async () => await OpenGroupConversationAsync());

        // Methode zum initialen Laden der Gruppen-Daten
        public async Task LoadGroupEventDataAsync(Guid entryId)
        {
            try
            {
                var rolesTask = _supabaseClient.From<GroupEventRole>().Where(r => r.EntryId == entryId).Get();
                var participantsTask = _supabaseClient.From<GroupEventParticipant>().Where(p => p.EntryId == entryId).Get();
                
                await Task.WhenAll(rolesTask, participantsTask);

                var roles = rolesTask.Result.Models;
                var participants = participantsTask.Result.Models;

                var userIds = participants.Select(p => p.UserId).Distinct().ToList();
                var usersResponse = await _supabaseClient.Rpc("get_public_users_batch", new Dictionary<string, object>
                {
                    { "p_user_ids", userIds }
                });

                var users = string.IsNullOrWhiteSpace(usersResponse?.Content)
                    ? new Dictionary<Guid, PublicUserProfile>()
                    : (JsonConvert.DeserializeObject<List<PublicUserProfile>>(usersResponse.Content) ?? new List<PublicUserProfile>())
                        .ToDictionary(u => u.Id);

                // Initialize properties inside Models
                foreach (var p in participants)
                {
                    if (users.TryGetValue(p.UserId, out var userData))
                    {
                        p.Username = userData.Username;
                        p.ProfileImageUrl = string.IsNullOrWhiteSpace(userData.ProfileImageUrl)
                            ? "profile_placeholder.png"
                            : _supabaseClient.Storage.From("profile_pictures").GetPublicUrl(userData.ProfileImageUrl);
                    }

                    if (p.RoleId.HasValue)
                    {
                        var roleMatch = roles.FirstOrDefault(r => r.Id == p.RoleId.Value);
                        if (roleMatch != null) p.RoleName = roleMatch.RoleName;
                    }
                }

                PendingApplicants = participants.Where(p => p.Status == "pending").ToList();
                ConfirmedParticipants = participants.Where(p => p.Status == "confirmed").ToList();

                foreach (var role in roles)
                {
                    role.ConfirmedCount = ConfirmedParticipants.Count(p => p.RoleId == role.Id);
                    role.PendingCount = PendingApplicants.Count(p => p.RoleId == role.Id);
                }
                
                EventRoles = roles;

                var currentUserIdStr = _authService.GetCurrentUserId();
                if (Guid.TryParse(currentUserIdStr, out var currentUserId))
                {
                    var myStatus = participants.FirstOrDefault(p => p.UserId == currentUserId);
                    if (myStatus != null)
                    {
                        IsCurrentUserParticipant = true;
                        CurrentUserStatus = myStatus.Status;
                    }
                    else
                    {
                        IsCurrentUserParticipant = false;
                        CurrentUserStatus = string.Empty;
                    }
                }

                OnPropertyChanged(nameof(TotalConfirmedCount));
                OnPropertyChanged(nameof(GeneralFillPercentage));
                OnPropertyChanged(nameof(CanApply));
                OnPropertyChanged(nameof(CanWithdrawApplication));
                OnPropertyChanged(nameof(CurrentUserStatusText));
                OnPropertyChanged(nameof(HasPendingApplicants));
                OnPropertyChanged(nameof(HasConfirmedParticipants));
                OnPropertyChanged(nameof(IsOwner));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler in LoadGroupEventDataAsync");
            }
        }

        private async Task OpenGroupConversationAsync()
        {
            if (SelectedEntry == null)
            {
                return;
            }

            try
            {
                var response = await _supabaseClient.Rpc("open_or_create_event_group_conversation_safe", new Dictionary<string, object>
                {
                    { "p_entry_id", SelectedEntry.id }
                });

                var conversationId = string.IsNullOrWhiteSpace(response?.Content)
                    ? Guid.Empty
                    : JsonConvert.DeserializeObject<Guid>(response.Content);

                if (conversationId == Guid.Empty)
                {
                    await ToastService.ShowWarningAsync("Gruppenchat konnte nicht gestartet werden.");
                    return;
                }

                await Shell.Current.GoToAsync($"ChatPage?chatId={conversationId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Öffnen des Gruppenchats");
                await ToastService.ShowErrorAsync("Gruppenchat konnte nicht geöffnet werden.");
            }
        }

        private async Task ApplyAsParticipantAsync(Guid? roleId)
        {
            if (SelectedEntry == null) return;
            var currentUserIdStr = _authService.GetCurrentUserId();
            if (!Guid.TryParse(currentUserIdStr, out var currentUserId)) return;

            var participant = new GroupEventParticipant
            {
                Id = Guid.NewGuid(),
                EntryId = SelectedEntry.id,
                UserId = currentUserId,
                RoleId = roleId,
                Status = "pending",
                AppliedAt = DateTime.UtcNow
            };

            try
            {
                await _supabaseClient.From<GroupEventParticipant>().Insert(participant);
                await LoadGroupEventDataAsync(SelectedEntry.id);
                OnPropertyChanged(nameof(CanWithdrawApplication));
                
                if (!Guid.TryParse(SelectedEntry.user_id, out var ownerGuid))
                {
                    _logger.LogWarning("ApplyAsParticipantAsync: Ungültige Owner-ID in SelectedEntry.user_id");
                    await ToastService.ShowErrorAsync(UserFriendlyErrors.Messages.ApplicationError);
                    return;
                }
                var partnerName = _authService.GetCurrentUserEmail();

                await _notificationService.AddNotificationAsync(
                    userId: ownerGuid,
                    message: $"{partnerName} hat sich für dein Event '{SelectedEntry.title}' beworben.",
                    type: "group_event_applied",
                    actionData: $"{{ \"event_id\":\"{SelectedEntry.id}\" }}"
                );

                await ToastService.ShowSuccessAsync(UserFriendlyErrors.Messages.ApplicationSent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler bei ApplyAsParticipantAsync");
                await ToastService.ShowErrorAsync(UserFriendlyErrors.Messages.ApplicationError);
            }
        }

        private async Task ConfirmApplicantAsync(Guid participantId)
        {
            if (SelectedEntry == null) return;
            try
            {
                var p = PendingApplicants.FirstOrDefault(x => x.Id == participantId);
                if (p == null) return;

                await _supabaseClient.From<GroupEventParticipant>()
                            .Where(x => x.Id == participantId)
                            .Set(x => x.Status, "confirmed")
                            .Set(x => x.ConfirmedAt, DateTime.UtcNow)
                            .Update();

                await LoadGroupEventDataAsync(SelectedEntry.id);
                OnPropertyChanged(nameof(HasPendingApplicants));

                var ownerName = _authService.GetCurrentUserEmail();
                await _notificationService.AddNotificationAsync(
                    userId: p.UserId,
                    message: $"{ownerName} hat deine Bewerbung für '{SelectedEntry.title}' bestätigt.",
                    type: "group_event_confirmed",
                    actionData: $"{{ \"event_id\":\"{SelectedEntry.id}\" }}"
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler bei ConfirmApplicantAsync");
            }
        }

        private async Task RejectApplicantAsync(Guid participantId)
        {
            if (SelectedEntry == null) return;
            try
            {
                var p = PendingApplicants.FirstOrDefault(x => x.Id == participantId);
                if (p == null) return;

                await _supabaseClient.From<GroupEventParticipant>()
                            .Where(x => x.Id == participantId)
                            .Set(x => x.Status, "rejected")
                            .Update();

                await LoadGroupEventDataAsync(SelectedEntry.id);
                OnPropertyChanged(nameof(HasPendingApplicants));
                
                var ownerName = _authService.GetCurrentUserEmail();
                await _notificationService.AddNotificationAsync(
                    userId: p.UserId,
                    message: $"{ownerName} hat deine Bewerbung für '{SelectedEntry.title}' abgelehnt.",
                    type: "group_event_rejected",
                    actionData: $"{{ \"event_id\":\"{SelectedEntry.id}\" }}"
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler bei RejectApplicantAsync");
            }
        }

        private async Task WithdrawApplicationAsync()
        {
            if (SelectedEntry == null) return;
            var currentUserIdStr = _authService.GetCurrentUserId();
            if (!Guid.TryParse(currentUserIdStr, out var currentUserId)) return;

            try
            {
                await _supabaseClient.From<GroupEventParticipant>()
                            .Where(x => x.EntryId == SelectedEntry.id && x.UserId == currentUserId)
                            .Delete();
                            
                await LoadGroupEventDataAsync(SelectedEntry.id);
                OnPropertyChanged(nameof(CanWithdrawApplication));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler bei WithdrawApplicationAsync");
            }
        }
    }
}
