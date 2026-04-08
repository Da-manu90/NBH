using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBH.Resources.Models;

namespace NBH.Services
{
    public class ModerationService
    {
        private readonly Supabase.Client _supabase;
        private readonly AuthService _authService;
        private readonly ILogger<ModerationService> _logger;
        private const string BlockedUsersKey = "blocked_users";

        public ModerationService(Supabase.Client supabase, AuthService authService, ILogger<ModerationService> logger)
        {
            _supabase = supabase;
            _authService = authService;
            _logger = logger;
        }

        /// <summary>
        /// Nutzer melden – erstellt einen Report-Eintrag in Supabase.
        /// </summary>
        public async Task<bool> ReportUserAsync(Guid reportedUserId, string reason, string? description)
        {
            try
            {
                var currentUserId = _authService.GetCurrentUserId();
                if (string.IsNullOrEmpty(currentUserId)) return false;

                if (!Guid.TryParse(currentUserId, out var reporterId))
                {
                    _logger.LogWarning("ReportUserAsync: Ungueltige Reporter-ID '{ReporterId}'", currentUserId);
                    return false;
                }

                var report = new Report
                {
                    ReporterId = reporterId,
                    ReportedUserId = reportedUserId,
                    Reason = reason,
                    Description = description,
                    Status = "pending",
                    CreatedAt = DateTime.UtcNow
                };

                await _supabase.From<Report>().Insert(report);
                _logger.LogInformation("User {UserId} reported by {ReporterId}", reportedUserId, currentUserId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Melden des Nutzers {UserId}", reportedUserId);
                return false;
            }
        }

        /// <summary>
        /// Eintrag melden – erstellt einen Report-Eintrag in Supabase.
        /// </summary>
        public async Task<bool> ReportEntryAsync(Guid reportedEntryId, string reason, string? description)
        {
            try
            {
                var currentUserId = _authService.GetCurrentUserId();
                if (string.IsNullOrEmpty(currentUserId)) return false;

                if (!Guid.TryParse(currentUserId, out var reporterId))
                {
                    _logger.LogWarning("ReportEntryAsync: Ungueltige Reporter-ID '{ReporterId}'", currentUserId);
                    return false;
                }

                var report = new Report
                {
                    ReporterId = reporterId,
                    ReportedEntryId = reportedEntryId,
                    Reason = reason,
                    Description = description,
                    Status = "pending",
                    CreatedAt = DateTime.UtcNow
                };

                await _supabase.From<Report>().Insert(report);
                _logger.LogInformation("Entry {EntryId} reported by {ReporterId}", reportedEntryId, currentUserId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Melden des Eintrags {EntryId}", reportedEntryId);
                return false;
            }
        }

        /// <summary>
        /// Nutzer lokal blockieren (via Preferences).
        /// </summary>
        public void BlockUser(Guid blockedUserId)
        {
            var blocked = GetBlockedUsers();
            if (!blocked.Contains(blockedUserId))
            {
                blocked.Add(blockedUserId);
                SaveBlockedUsers(blocked);
                _logger.LogInformation("User {UserId} blockiert (lokal)", blockedUserId);
            }
        }

        /// <summary>
        /// Prüft ob ein Nutzer lokal blockiert ist.
        /// </summary>
        public bool IsUserBlocked(Guid userId)
        {
            return GetBlockedUsers().Contains(userId);
        }

        /// <summary>
        /// Alle blockierten User-IDs abrufen.
        /// </summary>
        public List<Guid> GetBlockedUsers()
        {
            try
            {
                var json = Preferences.Get(BlockedUsersKey, "[]");
                return JsonSerializer.Deserialize<List<Guid>>(json) ?? new List<Guid>();
            }
            catch
            {
                return new List<Guid>();
            }
        }

        /// <summary>
        /// Blockierung eines Nutzers aufheben.
        /// </summary>
        public void UnblockUser(Guid userId)
        {
            var blocked = GetBlockedUsers();
            if (blocked.Remove(userId))
            {
                SaveBlockedUsers(blocked);
                _logger.LogInformation("User {UserId} entblockt", userId);
            }
        }

        private void SaveBlockedUsers(List<Guid> blockedUsers)
        {
            var json = JsonSerializer.Serialize(blockedUsers);
            Preferences.Set(BlockedUsersKey, json);
        }
    }
}
