using Mopups.Services;
using System.Text;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Windows.Input;
using Newtonsoft.Json;
using NBH.Resources.Models;
using NBH.Services;

namespace NBH.Resources.Views
{
    public partial class ChatReviewMopup : Mopups.Pages.PopupPage
    {
        private readonly Guid _chatId;
        private readonly Guid _reviewerId;
        private readonly Guid _reviewedId;

        private readonly HttpClient _httpClient;
        private readonly AuthService _authService;

        // Zum Speichern der aktuellen Ratings
        private int _friendlinessRating;
        private int _responseTimeRating;
        private int _helpfulnessRating;

        // Konstruktor ohne Parameter (f�r MAUI)
        public ChatReviewMopup()
        {
            InitializeComponent();
        }

        // Konstruktor mit Parametern
        public ChatReviewMopup(Guid chatId, Guid reviewerId, Guid reviewedId, AuthService authService)
        {
            InitializeComponent();

            _chatId = chatId;
            _reviewerId = reviewerId;
            _reviewedId = reviewedId;
            _authService = authService;

            // HttpClient vorbereiten
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri("https://entpyczncbnavvudfgrb.supabase.co/rest/v1/")
            };

            // Initialisiere Ratings
            _friendlinessRating = 0;
            _responseTimeRating = 0;
            _helpfulnessRating = 0;

            // Lade Username & aktualisiere Frage-Texte
            LoadReviewedUserData();
        }

        // L�dt den User, um z.B. den Namen anzuzeigen (Question1,2,3)
        private async void LoadReviewedUserData()
        {
            try
            {
                var userJwt = await SecureStorage.GetAsync("jwt_token");
                if (string.IsNullOrEmpty(userJwt))
                {
                    userJwt = await _authService.GetUserAccessTokenAsync();
                    await SecureStorage.SetAsync("jwt_token", userJwt);
                }
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", userJwt);
                _httpClient.DefaultRequestHeaders.Add("apikey", AppConfig.SupabaseAnonKey);

                var url = $"users?id=eq.{_reviewedId}";
                var response = await _httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var users = JsonConvert.DeserializeObject<UserProfile[]>(json);
                    if (users != null && users.Length > 0)
                    {
                        var name = users[0].Username ?? "Benutzer";

                        // Frage-Texte aktualisieren
                        Question1Label.Text = $"Wie freundlich war {name}?";
                        Question2Label.Text = $"Wie schnell hat {name} geantwortet?";
                        Question3Label.Text = $"Konnte {name} Ihnen helfen?";
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Laden der User-Daten fehlgeschlagen: {ex.Message}");
            }
        }

        // -----------------------------
        // Klick-Handler f�r Sterne
        // -----------------------------
        private void OnFriendlinessStarClicked(object sender, EventArgs e)
        {
            if (sender is ImageButton btn && btn.CommandParameter is string param && int.TryParse(param, out int rating))
            {
                _friendlinessRating = rating;
                UpdateFriendlinessStars();
            }
        }

        private void OnResponseTimeStarClicked(object sender, EventArgs e)
        {
            if (sender is ImageButton btn && btn.CommandParameter is string param && int.TryParse(param, out int rating))
            {
                _responseTimeRating = rating;
                UpdateResponseTimeStars();
            }
        }

        private void OnHelpfulnessStarClicked(object sender, EventArgs e)
        {
            if (sender is ImageButton btn && btn.CommandParameter is string param && int.TryParse(param, out int rating))
            {
                _helpfulnessRating = rating;
                UpdateHelpfulnessStars();
            }
        }

        // -----------------------------
        // Update-Methoden
        // -----------------------------
        private void UpdateFriendlinessStars()
        {
            // F�llt die passenden Sterne, je nach _friendlinessRating
            FriendlinessStar1.Source = (_friendlinessRating >= 1) ? "star_filled32.png" : "star_empty32.png";
            FriendlinessStar2.Source = (_friendlinessRating >= 2) ? "star_filled32.png" : "star_empty32.png";
            FriendlinessStar3.Source = (_friendlinessRating >= 3) ? "star_filled32.png" : "star_empty32.png";
            FriendlinessStar4.Source = (_friendlinessRating >= 4) ? "star_filled32.png" : "star_empty32.png";
            FriendlinessStar5.Source = (_friendlinessRating >= 5) ? "star_filled32.png" : "star_empty32.png";
        }

        private void UpdateResponseTimeStars()
        {
            ResponseTimeStar1.Source = (_responseTimeRating >= 1) ? "star_filled32.png" : "star_empty32.png";
            ResponseTimeStar2.Source = (_responseTimeRating >= 2) ? "star_filled32.png" : "star_empty32.png";
            ResponseTimeStar3.Source = (_responseTimeRating >= 3) ? "star_filled32.png" : "star_empty32.png";
            ResponseTimeStar4.Source = (_responseTimeRating >= 4) ? "star_filled32.png" : "star_empty32.png";
            ResponseTimeStar5.Source = (_responseTimeRating >= 5) ? "star_filled32.png" : "star_empty32.png";
        }

        private void UpdateHelpfulnessStars()
        {
            HelpfulnessStar1.Source = (_helpfulnessRating >= 1) ? "star_filled32.png" : "star_empty32.png";
            HelpfulnessStar2.Source = (_helpfulnessRating >= 2) ? "star_filled32.png" : "star_empty32.png";
            HelpfulnessStar3.Source = (_helpfulnessRating >= 3) ? "star_filled32.png" : "star_empty32.png";
            HelpfulnessStar4.Source = (_helpfulnessRating >= 4) ? "star_filled32.png" : "star_empty32.png";
            HelpfulnessStar5.Source = (_helpfulnessRating >= 5) ? "star_filled32.png" : "star_empty32.png";
        }

        // -----------------------------
        // Abschicken-Button
        // -----------------------------
        private async void OnSubmitButtonClicked(object sender, EventArgs e)
        {
            // Falls 0 nicht erlaubt, setze Minimalwert 1
            int finalFriendliness = (_friendlinessRating < 1) ? 1 : _friendlinessRating;
            int finalResponseTime = (_responseTimeRating < 1) ? 1 : _responseTimeRating;
            int finalHelpfulness = (_helpfulnessRating < 1) ? 1 : _helpfulnessRating;

            var review = new
            {
                chat_id = _chatId,
                reviewer_id = _reviewerId,
                reviewed_id = _reviewedId,
                friendliness = finalFriendliness,
                response_time = finalResponseTime,
                helpfulness = finalHelpfulness,
                created_at = DateTime.UtcNow
            };

            var json = JsonConvert.SerializeObject(review);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("chat_reviews", content);

            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine("Bewertung erfolgreich gespeichert.");
                await Mopups.Services.MopupService.Instance.PopAsync();
            }
            else
            {
                var err = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Fehler beim Speichern der Bewertung: {response.StatusCode} - {response.ReasonPhrase}. Details: {err}");
            }
        }

        // -----------------------------
        // Schlie�en-Button
        // -----------------------------
        private async void OnCloseButtonClicked(object sender, EventArgs e)
        {
            var button = (Button)sender;
            await button.ScaleTo(0.9, 100);
            await button.ScaleTo(1.0, 100);

            await Mopups.Services.MopupService.Instance.PopAsync();
        }
    }
}
