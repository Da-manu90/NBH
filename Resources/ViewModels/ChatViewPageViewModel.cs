using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using NBH.Resources.Models;
using NBH.Services;
using Supabase;
using Supabase.Postgrest;
using static Supabase.Postgrest.Constants;

namespace NBH.Resources.ViewModels
{
    public class ChatViewPageViewModel : BaseViewModel
    {
        private readonly ILogger<ChatViewPageViewModel> _logger;
        private readonly AuthService _authService;
        private readonly Supabase.Client _supabaseClient;


        public ObservableCollection<PublicChat> Chats { get; set; } = new();
        
        private bool _isLoading;
        public bool IsLoading 
        { 
            get => _isLoading; 
            set { _isLoading = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsNotLoading)); } 
        }
        public bool IsNotLoading => !IsLoading;

        public ICommand RefreshCommand { get; }
        public ICommand ArchiveCommand { get; }

        // --------------------------------------------------------
        // Konstruktor
        // --------------------------------------------------------
        public ChatViewPageViewModel(
            ILogger<ChatViewPageViewModel> logger,
            AuthService authService,
            Supabase.Client supabaseClient
        )
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            _supabaseClient = supabaseClient ?? throw new ArgumentNullException(nameof(supabaseClient));

            RefreshCommand = new Command(async () => await LoadChatsAsync());
            ArchiveCommand = new Command<PublicChat>(async (chat) => await ArchiveChatAsync(chat));
        }

        // --------------------------------------------------------
        // Chats direkt aus der 'chats'-Tabelle laden (kein RPC)
        // --------------------------------------------------------
        public async Task LoadChatsAsync()
        {
            try
            {
                IsLoading = true;
                _logger.LogInformation("[ChatViewPageViewModel] Lade Chats aus DB...");

                var userId = _authService.GetCurrentUserId();
                if (string.IsNullOrEmpty(userId)) return;

                // Chats als Ersteller laden (user_id == ich, nicht archiviert)
                var q1 = await _supabaseClient.From<PublicChat>()
                    .Filter("user_id", Operator.Equals, userId)
                    .Filter("archived_by_user", Operator.Equals, "false")
                    .Get().ConfigureAwait(false);

                // Chats als Partner laden (partner_id == ich, nicht archiviert)
                var q2 = await _supabaseClient.From<PublicChat>()
                    .Filter("partner_id", Operator.Equals, userId)
                    .Filter("archived_by_partner", Operator.Equals, "false")
                    .Get().ConfigureAwait(false);

                // Zusammenführen + sortieren
                var allChats = (q1.Models ?? new List<PublicChat>())
                    .Concat(q2.Models ?? new List<PublicChat>())
                    .OrderByDescending(c => c.LastMessageDate ?? c.CreatedAt)
                    .ToList();

                // Partnerdaten anreichern
                var enriched = new List<PublicChat>();
                foreach (var chat in allChats)
                {
                    var partnerId = (chat.UserId == userId) ? chat.PartnerId : chat.UserId;
                    var (partnerName, partnerImage) = await GetPartnerDataAsync(partnerId);
                    chat.PartnerName = partnerName;

                    if (!string.IsNullOrEmpty(partnerImage) && !partnerImage.StartsWith("http"))
                        chat.PartnerImageUrl = _supabaseClient.Storage.From("profile_pictures").GetPublicUrl(partnerImage);
                    else if (string.IsNullOrEmpty(partnerImage))
                        chat.PartnerImageUrl = "profile_placeholder.png";
                    else
                        chat.PartnerImageUrl = partnerImage;

                    // Ungelesen-Status berechnen
                    if (chat.LastMessageDate.HasValue)
                    {
                        chat.IsUnread = chat.UserId == userId
                            ? (chat.UserLastReadAt == null || chat.LastMessageDate > chat.UserLastReadAt)
                            : (chat.PartnerLastReadAt == null || chat.LastMessageDate > chat.PartnerLastReadAt);
                    }

                    enriched.Add(chat);
                }

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    Chats.Clear();
                    foreach (var chat in enriched)
                        Chats.Add(chat);
                });

                _logger.LogInformation("[ChatViewPageViewModel] {Count} Chats geladen.", enriched.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ChatViewPageViewModel] Fehler beim Laden der Chats.");
            }
            finally
            {
                IsLoading = false;
            }
        }

        // --------------------------------------------------------
        // Chat l�schen// Archivieren
        // --------------------------------------------------------
        private async Task ArchiveChatAsync(PublicChat chat)
        {
            if (chat == null)
                return;

            try
            {
                // Aktuellen User ermitteln
                var userId = _authService.GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                    return; // kein User -> Abbruch

                // Wenn der aktuelle User der "Ersteller" ist
                if (chat.UserId == userId)
                {
                    chat.ArchivedByUser = true; // DB-Spalte
                }
                // Wenn der aktuelle User der "Partner" ist
                else if (chat.PartnerId == userId)
                {
                    chat.ArchivedByPartner = true;
                }

                // Jetzt updaten:
                // Achtung: In manchen Versionen musst du an den Update() das Objekt direkt �bergeben:
                var updateResp = await _supabaseClient
                    .From<PublicChat>()
                    .Update(chat);
                // .Single(); // Mglw. brauchst du .Single(), je nach Supabase-Version.

                // Falls die Supabase-Library hier ein Ergebnis zur�ckgibt:
                if (updateResp?.Models.Any() == true)
                {
                    // Aus der lokalen Liste entfernen => "verschwindet" f�r DICH
                    if (Chats.Contains(chat))
                        Chats.Remove(chat);

                    _logger.LogInformation($"Chat {chat.Id} archiviert f�r User {userId}.");
                }
                else
                {
                    _logger.LogWarning("Keine R�ckgabe beim Updaten: Chat {0} wurde evtl. nicht archiviert.", chat.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Archivieren von Chat {0}.", chat.Id);
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await ToastService.ShowErrorAsync(UserFriendlyErrors.Messages.ChatArchiveError);
                });
            }
        }



        // --------------------------------------------------------
        // Partnerdaten (Name, Profilbild) laden
        // --------------------------------------------------------
        private async Task<(string name, string imageFile)> GetPartnerDataAsync(string partnerId)
        {
            if (string.IsNullOrWhiteSpace(partnerId))
                return ("Unbekannt", string.Empty);

            try
            {
                // Falls User-ID Guid-basiert ist, k�nntest du sie ggf. parsen
                if (!Guid.TryParse(partnerId, out var partnerGuid))
                    return ("Unbekannt", string.Empty);

                // User-Objekt �ber die ID abrufen
                var partnerResp = await _supabaseClient
                    .From<User>()
                    .Where(u => u.Id == partnerGuid)
                    .Get()
                    .ConfigureAwait(false);

                var partner = partnerResp.Models.FirstOrDefault();
                if (partner != null)
                {
                    return (
                        partner.Username ?? "Unbekannt",
                        partner.ProfileImageUrl ?? string.Empty
                    );
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ChatViewPageViewModel] Fehler beim Laden der Partner-Daten.");
            }

            return ("Unbekannt", string.Empty);
        }
    }
}
