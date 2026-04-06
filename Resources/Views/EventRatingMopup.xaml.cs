using Mopups.Services;
using System.Text;
using System.Net.Http;
using System.Net.Http.Headers;
using Newtonsoft.Json;
using NBH.Services;
using NBH.Resources.Models;

namespace NBH.Resources.Views
{
    public partial class EventRatingMopup : Mopups.Pages.PopupPage
    {
        private readonly Guid _eventId;
        private readonly Guid _reviewerId;
        private readonly Guid _reviewedId;

        private readonly HttpClient _httpClient;
        private readonly AuthService _authService;

        // Ratings
        private int _eventFriendlinessRating;
        private int _descriptionAccuracyRating;
        private int _reliabilityRating;

        public EventRatingMopup()
        {
            InitializeComponent();
        }

        // Nehme an, dass du sowohl eventId als auch entryId hast
        // Falls du entry_id nicht brauchst, kannst du es entfernen
        public EventRatingMopup(Guid eventId, Guid reviewerId, Guid reviewedId, AuthService authService)
        {
            InitializeComponent();

            _eventId = eventId;
            _reviewerId = reviewerId;
            _reviewedId = reviewedId;
            _authService = authService;


            // HttpClient
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri("https://entpyczncbnavvudfgrb.supabase.co/rest/v1/")
            };

            // Initiale Ratings auf 0
            _eventFriendlinessRating = 0;
            _descriptionAccuracyRating = 0;
            _reliabilityRating = 0;

            LoadReviewedUserData();
        }

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

                        // Fragen anpassen
                        Question1Label.Text = $"Wie freundlich empfanden Sie {name} im pers�nlichen Kontakt?";
                        Question2Label.Text = $"War das Angebot / die Anfrage von {name} wie beschrieben?";
                        Question3Label.Text = $"Wie zuverl�ssig haben Sie {name} empfunden?";
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Laden der User-Daten fehlgeschlagen: {ex.Message}");
            }
        }

        // -----------------------------
        // Klick-Handler Sterne
        // -----------------------------
        private void OnEventFriendlinessStarClicked(object sender, EventArgs e)
        {
            if (sender is ImageButton btn &&
                btn.CommandParameter is string param &&
                int.TryParse(param, out int rating))
            {
                _eventFriendlinessRating = rating;
                UpdateEventFriendlinessStars();
            }
        }

        private void OnDescriptionAccuracyStarClicked(object sender, EventArgs e)
        {
            if (sender is ImageButton btn &&
                btn.CommandParameter is string param &&
                int.TryParse(param, out int rating))
            {
                _descriptionAccuracyRating = rating;
                UpdateDescriptionAccuracyStars();
            }
        }

        private void OnReliabilityStarClicked(object sender, EventArgs e)
        {
            if (sender is ImageButton btn &&
                btn.CommandParameter is string param &&
                int.TryParse(param, out int rating))
            {
                _reliabilityRating = rating;
                UpdateReliabilityStars();
            }
        }

        // -----------------------------
        // Sterne aktualisieren
        // -----------------------------
        private void UpdateEventFriendlinessStars()
        {
            EventFriendlinessStar1.Source = (_eventFriendlinessRating >= 1) ? "star_filled32.png" : "star_empty32.png";
            EventFriendlinessStar2.Source = (_eventFriendlinessRating >= 2) ? "star_filled32.png" : "star_empty32.png";
            EventFriendlinessStar3.Source = (_eventFriendlinessRating >= 3) ? "star_filled32.png" : "star_empty32.png";
            EventFriendlinessStar4.Source = (_eventFriendlinessRating >= 4) ? "star_filled32.png" : "star_empty32.png";
            EventFriendlinessStar5.Source = (_eventFriendlinessRating >= 5) ? "star_filled32.png" : "star_empty32.png";
        }

        private void UpdateDescriptionAccuracyStars()
        {
            DescriptionAccuracyStar1.Source = (_descriptionAccuracyRating >= 1) ? "star_filled32.png" : "star_empty32.png";
            DescriptionAccuracyStar2.Source = (_descriptionAccuracyRating >= 2) ? "star_filled32.png" : "star_empty32.png";
            DescriptionAccuracyStar3.Source = (_descriptionAccuracyRating >= 3) ? "star_filled32.png" : "star_empty32.png";
            DescriptionAccuracyStar4.Source = (_descriptionAccuracyRating >= 4) ? "star_filled32.png" : "star_empty32.png";
            DescriptionAccuracyStar5.Source = (_descriptionAccuracyRating >= 5) ? "star_filled32.png" : "star_empty32.png";
        }

        private void UpdateReliabilityStars()
        {
            ReliabilityStar1.Source = (_reliabilityRating >= 1) ? "star_filled32.png" : "star_empty32.png";
            ReliabilityStar2.Source = (_reliabilityRating >= 2) ? "star_filled32.png" : "star_empty32.png";
            ReliabilityStar3.Source = (_reliabilityRating >= 3) ? "star_filled32.png" : "star_empty32.png";
            ReliabilityStar4.Source = (_reliabilityRating >= 4) ? "star_filled32.png" : "star_empty32.png";
            ReliabilityStar5.Source = (_reliabilityRating >= 5) ? "star_filled32.png" : "star_empty32.png";
        }

        // -----------------------------
        // Abschicken-Button
        // -----------------------------
        private async void OnSubmitButtonClicked(object sender, EventArgs e)
        {
            var button = (Button)sender;
            await button.ScaleTo(0.9, 100);
            await button.ScaleTo(1.0, 100);
            // Falls 0 nicht erlaubt, setze Minimalwert 1
            int finalEventFriendliness = (_eventFriendlinessRating < 1) ? 1 : _eventFriendlinessRating;
            int finalDescriptionAccuracy = (_descriptionAccuracyRating < 1) ? 1 : _descriptionAccuracyRating;
            int finalReliability = (_reliabilityRating < 1) ? 1 : _reliabilityRating;

            // Kommentar aus Editor
            string commentText = CommentEditor.Text ?? string.Empty;

            var review = new
            {
                event_id = _eventId,
                reviewer_id = _reviewerId,
                reviewed_id = _reviewedId,
                event_friendliness = finalEventFriendliness,
                description_accuracy = finalDescriptionAccuracy,
                reliability = finalReliability,
                comment = commentText,        // NEU: Kommentar in DB
                created_at = DateTime.UtcNow
            };

            var json = JsonConvert.SerializeObject(review);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("event_rating", content);

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
            await Mopups.Services.MopupService.Instance.PopAsync();
        }
    }
}
