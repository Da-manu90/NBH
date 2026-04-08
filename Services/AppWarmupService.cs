using Microsoft.Extensions.Logging;
using NBH.Resources.Models;
using Newtonsoft.Json;

namespace NBH.Services
{
    public class AppWarmupService
    {
        private readonly AuthService _authService;
        private readonly Supabase.Client _supabaseClient;
        private readonly NotificationService _notificationService;
        private readonly ILogger<AppWarmupService> _logger;

        private readonly SemaphoreSlim _warmupLock = new(1, 1);
        private Guid? _cachedUserId;
        private UserProfileSummary? _cachedSummary;
        private DateTime _lastWarmupAt = DateTime.MinValue;

        public AppWarmupService(
            AuthService authService,
            Supabase.Client supabaseClient,
            NotificationService notificationService,
            ILogger<AppWarmupService> logger)
        {
            _authService = authService;
            _supabaseClient = supabaseClient;
            _notificationService = notificationService;
            _logger = logger;
        }

        public async Task WarmAuthenticatedAsync()
        {
            var currentUserId = _authService.GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(currentUserId) || !Guid.TryParse(currentUserId, out var userGuid))
            {
                return;
            }

            if (_cachedUserId == userGuid && (DateTime.UtcNow - _lastWarmupAt).TotalMinutes < 10)
            {
                return;
            }

            await _warmupLock.WaitAsync();
            try
            {
                if (_cachedUserId == userGuid && (DateTime.UtcNow - _lastWarmupAt).TotalMinutes < 10)
                {
                    return;
                }

                var summaryTask = _supabaseClient.Rpc("get_user_profile_summary", new Dictionary<string, object>
                {
                    { "p_user_id", userGuid }
                });

                var notificationTask = _notificationService.InitializeAsync();

                await Task.WhenAll(summaryTask, notificationTask);

                var summaryResponse = await summaryTask;
                if (!string.IsNullOrWhiteSpace(summaryResponse?.Content))
                {
                    _cachedSummary = JsonConvert.DeserializeObject<UserProfileSummary>(summaryResponse.Content);
                    _cachedUserId = userGuid;
                    _lastWarmupAt = DateTime.UtcNow;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "AppWarmup fehlgeschlagen.");
            }
            finally
            {
                _warmupLock.Release();
            }
        }

        public UserProfileSummary? GetCachedOwnProfileSummary(Guid userId)
        {
            if (_cachedUserId == userId)
            {
                return _cachedSummary;
            }

            return null;
        }

        public void Clear()
        {
            _cachedUserId = null;
            _cachedSummary = null;
            _lastWarmupAt = DateTime.MinValue;
        }
    }
}
