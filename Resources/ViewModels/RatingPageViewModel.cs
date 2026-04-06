using CommunityToolkit.Mvvm.ComponentModel;
using Supabase;

namespace NBH.Resources.ViewModels
{
    public partial class RatingPageViewModel : ObservableObject
    {
        private readonly Client _supabase;

        public RatingPageViewModel(Client supabase)
        {
            _supabase = supabase;
        }

        [ObservableProperty] private double chatHelpfulnessAverage;
        [ObservableProperty] private double chatFriendlinessAverage;
        [ObservableProperty] private double chatResponseTimeAverage;
        [ObservableProperty] private double eventFriendlinessAverage;
        [ObservableProperty] private double eventDescriptionAccuracyAverage;
        [ObservableProperty] private double eventReliabilityAverage;

        public async Task LoadDataAsync(Guid userId)
        {
            // Hier Ratings aus Supabase laden
            // z.B. var ratings = await _supabase.From<Rating>()
            //     .Where(r => r.ReviewedUserId == userId)
            //     .Get();
        }
    }
}