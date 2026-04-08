using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NBH.Resources.Models;
using Newtonsoft.Json;
using Supabase;
using System.Collections.ObjectModel;

namespace NBH.Resources.ViewModels
{
    public partial class RatingPageViewModel : ObservableObject
    {
        private readonly Client _supabase;
        private const int CommentPageSize = 4;

        private Guid _reviewedUserId;
        private int _commentOffset;
        private int _totalComments;

        public RatingPageViewModel(Client supabase)
        {
            _supabase = supabase;

            EventComments = new ObservableCollection<RatingCommentItem>();
            LoadPreviousCommentsCommand = new AsyncRelayCommand(LoadPreviousCommentsAsync);
            LoadNextCommentsCommand = new AsyncRelayCommand(LoadNextCommentsAsync);
        }

        [ObservableProperty] private string profileImageUrl = "profile_placeholder.png";
        [ObservableProperty] private string username = string.Empty;
        [ObservableProperty] private double chatHelpfulnessAverage;
        [ObservableProperty] private double chatFriendlinessAverage;
        [ObservableProperty] private double chatResponseTimeAverage;
        [ObservableProperty] private double eventFriendlinessAverage;
        [ObservableProperty] private double eventDescriptionAccuracyAverage;
        [ObservableProperty] private double eventReliabilityAverage;
        [ObservableProperty] private bool hasChatRating;
        [ObservableProperty] private bool hasEventRating;
        [ObservableProperty] private bool canGoBackInComments;
        [ObservableProperty] private bool canGoForwardInComments;

        public ObservableCollection<RatingCommentItem> EventComments { get; }

        public IAsyncRelayCommand LoadPreviousCommentsCommand { get; }
        public IAsyncRelayCommand LoadNextCommentsCommand { get; }

        public async Task LoadDataAsync(Guid userId)
        {
            _reviewedUserId = userId;

            if (userId == Guid.Empty)
            {
                ResetState();
                return;
            }

            _commentOffset = 0;
            await LoadProfileSummaryAsync(userId);

            if (!await LoadDetailedRatingsAsync(userId))
            {
                await LoadRatingStatsFallbackAsync(userId);
                LoadPlaceholderComments();
            }
        }

        private async Task LoadProfileSummaryAsync(Guid userId)
        {
            try
            {
                var response = await _supabase.Rpc("get_user_profile_summary", new Dictionary<string, object>
                {
                    { "p_user_id", userId }
                });

                if (string.IsNullOrWhiteSpace(response?.Content))
                    return;

                var summary = JsonConvert.DeserializeObject<UserProfileSummary>(response.Content);
                if (summary?.User == null)
                    return;

                Username = summary.User.Username ?? string.Empty;
                ProfileImageUrl = string.IsNullOrWhiteSpace(summary.User.ProfileImageUrl)
                    ? "profile_placeholder.png"
                    : _supabase.Storage.From("profile_pictures").GetPublicUrl(summary.User.ProfileImageUrl);
            }
            catch
            {
            }
        }

        private async Task<bool> LoadDetailedRatingsAsync(Guid userId)
        {
            try
            {
                var response = await _supabase.Rpc("get_user_rating_page_safe", new Dictionary<string, object>
                {
                    { "p_user_id", userId },
                    { "p_limit", CommentPageSize },
                    { "p_offset", _commentOffset }
                });

                if (string.IsNullOrWhiteSpace(response?.Content))
                    return false;

                var details = JsonConvert.DeserializeObject<RatingPageDetailsResponse>(response.Content);
                if (details == null)
                    return false;

                var chat = details.Chat ?? new RatingAverageSection();
                var @event = details.Event ?? new RatingAverageSection();

                ChatHelpfulnessAverage = Math.Round(chat.HelpfulnessAverage, 1);
                ChatFriendlinessAverage = Math.Round(chat.FriendlinessAverage, 1);
                ChatResponseTimeAverage = Math.Round(chat.ResponseTimeAverage, 1);

                EventFriendlinessAverage = Math.Round(@event.FriendlinessAverage, 1);
                EventDescriptionAccuracyAverage = Math.Round(@event.DescriptionAccuracyAverage, 1);
                EventReliabilityAverage = Math.Round(@event.ReliabilityAverage, 1);

                HasChatRating = chat.TotalCount > 0;
                HasEventRating = @event.TotalCount > 0;

                _totalComments = details.TotalComments;
                EventComments.Clear();
                foreach (var item in details.Comments ?? new List<RatingCommentItem>())
                {
                    EventComments.Add(item);
                }

                UpdateCommentPagingState();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private async Task LoadRatingStatsFallbackAsync(Guid userId)
        {
            try
            {
                var response = await _supabase.Rpc("get_user_rating_stats", new Dictionary<string, object>
                {
                    { "p_user_id", userId }
                });

                if (string.IsNullOrWhiteSpace(response?.Content))
                    return;

                var stats = JsonConvert.DeserializeObject<RatingStats>(response.Content);
                var chatAvg = Math.Round(stats?.ChatAvg ?? 0.0, 1);
                var eventAvg = Math.Round(stats?.EventAvg ?? 0.0, 1);

                ChatHelpfulnessAverage = chatAvg;
                ChatFriendlinessAverage = chatAvg;
                ChatResponseTimeAverage = chatAvg;

                EventFriendlinessAverage = eventAvg;
                EventDescriptionAccuracyAverage = eventAvg;
                EventReliabilityAverage = eventAvg;

                HasChatRating = chatAvg > 0;
                HasEventRating = eventAvg > 0;
            }
            catch
            {
                ResetState();
            }
        }

        private async Task LoadPreviousCommentsAsync()
        {
            if (_reviewedUserId == Guid.Empty || _commentOffset <= 0)
                return;

            _commentOffset = Math.Max(0, _commentOffset - CommentPageSize);
            await LoadDetailedRatingsAsync(_reviewedUserId);
        }

        private async Task LoadNextCommentsAsync()
        {
            if (_reviewedUserId == Guid.Empty || _commentOffset + CommentPageSize >= _totalComments)
                return;

            _commentOffset += CommentPageSize;
            await LoadDetailedRatingsAsync(_reviewedUserId);
        }

        private void LoadPlaceholderComments()
        {
            EventComments.Clear();
            UpdateCommentPagingState();
        }

        private void UpdateCommentPagingState()
        {
            CanGoBackInComments = _commentOffset > 0;
            CanGoForwardInComments = _commentOffset + EventComments.Count < _totalComments;
        }

        private void ResetState()
        {
            Username = string.Empty;
            ProfileImageUrl = "profile_placeholder.png";
            ChatHelpfulnessAverage = 0;
            ChatFriendlinessAverage = 0;
            ChatResponseTimeAverage = 0;
            EventFriendlinessAverage = 0;
            EventDescriptionAccuracyAverage = 0;
            EventReliabilityAverage = 0;
            HasChatRating = false;
            HasEventRating = false;
            _totalComments = 0;
            _commentOffset = 0;
            EventComments.Clear();
            UpdateCommentPagingState();
        }
    }
}
