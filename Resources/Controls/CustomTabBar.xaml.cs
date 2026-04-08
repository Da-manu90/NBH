// ============================================================
// CustomTabBar.xaml.cs – Animierte TabBar mit Glow-Slider
// ============================================================
// Pfad: /NBH/Resources/Controls/CustomTabBar.xaml.cs
//
// ARCHITEKTUR:
// - 5 Tabs mit je einem Icon + Label
// - Ein "Slider" (Border) gleitet per TranslationX zum aktiven Tab
// - Ein "Dot" (BoxView) bewegt sich synchron mit
// - Glow-Effekt über Shadow.Opacity auf dem aktiven Icon
// - Federnde Animation via Easing.SpringOut
// - Haptic Feedback bei jedem Tap
// - Tab-Wechsel löst Shell-Navigation aus
// ============================================================

using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices;
using NBH.Resources.Models;
using NBH.Services;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Threading.Tasks;
#if IOS
using NBH.Platforms.iOS;
#endif

namespace NBH.Resources.Controls
{
    public partial class CustomTabBar : ContentView
    {
        // ==========================================================
        // Konfiguration
        // ==========================================================

        /// <summary>
        /// Akzentfarbe pro Tab (Glow, Label, Slider-Tint, Dot).
        /// Reihenfolge: Profil, Karte, Neu, Chat, FAQ
        /// </summary>
        private static readonly Color[] TabColors = new[]
        {
            Color.FromArgb("#e4f8f5"),  // Tab 0: Profil – Eisgrün
            Color.FromArgb("#378ADD"),  // Tab 1: Karte  – Blau
            Color.FromArgb("#00d2a8"),  // Tab 2: Neu    – Teal-Grün
            Color.FromArgb("#D4537E"),  // Tab 3: Chat   – Pink
            Color.FromArgb("#00897b"),  // Tab 4: FAQ    – Dunkles Teal
        };

        /// <summary>
        /// Shell-Routen für die Navigation.
        /// Muss mit der TabBar-Reihenfolge übereinstimmen.
        /// </summary>
        private static readonly string[] TabRoutes = new[]
        {
            "//MainTabs/ProfileTab/ProfilePage",
            "//MainTabs/MapTab/MapPage",
            "//MainTabs/AddTab/AddPage",
            "//MainTabs/ChatTab/ChatViewPage",
            "//MainTabs/FaqTab/FAQPage",
        };

        /// <summary>
        /// Farbe für inaktive Icons/Labels.
        /// </summary>
        private static readonly Color InactiveColor = Color.FromArgb("#8a8a8a");

        /// <summary>
        /// Animationsdauer in Millisekunden.
        /// </summary>
        private const uint AnimationDuration = 400;

        // ==========================================================
        // State
        // ==========================================================

        private int _currentIndex = -1;
        private bool _isAnimating = false;
        private bool _isLayoutReady = false;
        private readonly Supabase.Client? _supabaseClient;
        private readonly AuthService? _authService;
        private readonly PhoenixRealtimeService? _phoenixRealtimeService;

        // Referenzen auf alle Icons, Labels und Glows (für Schleifen)
        private Image[] _icons;
        private Label[] _labels;
        private Shadow[] _glows;

        // ==========================================================
        // BindableProperty: SelectedIndex
        // Erlaubt es, den aktiven Tab von außen zu setzen, z.B.
        // beim Erscheinen einer Seite.
        // ==========================================================
        public static readonly BindableProperty SelectedIndexProperty =
            BindableProperty.Create(
                nameof(SelectedIndex),
                typeof(int),
                typeof(CustomTabBar),
                0,
                propertyChanged: OnSelectedIndexChanged);

        public int SelectedIndex
        {
            get => (int)GetValue(SelectedIndexProperty);
            set => SetValue(SelectedIndexProperty, value);
        }

        private static void OnSelectedIndexChanged(BindableObject bindable, object oldValue, object newValue)
        {
            if (bindable is CustomTabBar tabBar && newValue is int index)
            {
                tabBar.SelectTabInternal(index, animate: tabBar._isLayoutReady);
            }
        }

        // ==========================================================
        // Konstruktor
        // ==========================================================

        public CustomTabBar()
        {
            InitializeComponent();

            try
            {
                var serviceProvider = ((App)Application.Current!).GetServiceProvider();
                _supabaseClient = serviceProvider.GetService(typeof(Supabase.Client)) as Supabase.Client;
                _authService = serviceProvider.GetService(typeof(AuthService)) as AuthService;
                _phoenixRealtimeService = serviceProvider.GetService(typeof(PhoenixRealtimeService)) as PhoenixRealtimeService;
            }
            catch
            {
                _supabaseClient = null;
                _authService = null;
                _phoenixRealtimeService = null;
            }

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private async void OnLoaded(object? sender, EventArgs e)
        {
            if (_phoenixRealtimeService != null)
            {
                _phoenixRealtimeService.PostgresChanged -= OnRealtimePostgresChanged;
                _phoenixRealtimeService.PostgresChanged += OnRealtimePostgresChanged;
            }

            await RefreshUnreadChatBadgeAsync();
        }

        private void OnUnloaded(object? sender, EventArgs e)
        {
            if (_phoenixRealtimeService != null)
            {
                _phoenixRealtimeService.PostgresChanged -= OnRealtimePostgresChanged;
            }
        }

        private async void OnRealtimePostgresChanged(object? sender, PhoenixPostgresEvent e)
        {
            if (e.Table == "chat_messages" || e.Table == "conversation_messages")
            {
                if (!ShouldRefreshForIncomingMessage(e))
                {
                    return;
                }

                await RefreshUnreadChatBadgeAsync();
                return;
            }

            if (e.Table == "chats" || e.Table == "conversations")
            {
                await RefreshUnreadChatBadgeAsync();
            }
        }

        private bool ShouldRefreshForIncomingMessage(PhoenixPostgresEvent e)
        {
            if (_authService == null)
            {
                return true;
            }

            var me = _authService.GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(me))
            {
                return true;
            }

            var eventType = e.EventType?.ToUpperInvariant() ?? string.Empty;
            if (eventType != "INSERT")
            {
                return true;
            }

            if (e.New == null || !TryGetRecordValue(e.New, "sender_id", out var senderId))
            {
                return true;
            }

            // Wichtig: Eigene gesendete Nachricht soll den "neu"-Badge nicht triggern.
            return !string.Equals(senderId, me, StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryGetRecordValue(System.Collections.Generic.Dictionary<string, object> record, string key, out string value)
        {
            value = string.Empty;
            if (record == null || !record.TryGetValue(key, out var raw) || raw == null)
            {
                return false;
            }

            value = raw.ToString() ?? string.Empty;
            return !string.IsNullOrWhiteSpace(value);
        }

        // ==========================================================
        // Lifecycle: Layout ist fertig → Slider initial positionieren
        // ==========================================================

        protected override void OnSizeAllocated(double width, double height)
        {
            base.OnSizeAllocated(width, height);

            if (width > 0 && !_isLayoutReady)
            {
                InitializeReferences();

                // Auf das Layout des INNEREN Grids warten
                TabItemsGrid.SizeChanged += OnTabItemsGridSizeChanged;
            }
        }

        private void OnTabItemsGridSizeChanged(object sender, EventArgs e)
        {
            if (TabItemsGrid.Width <= 0)
                return;

            // Nur einmal feuern
            TabItemsGrid.SizeChanged -= OnTabItemsGridSizeChanged;
            _isLayoutReady = true;

            Dispatcher.Dispatch(() =>
            {
                SelectTabInternal(SelectedIndex, animate: false);
            });
        }

        /// <summary>
        /// Baut die Arrays aus XAML-Referenzen auf, damit wir
        /// per Index zugreifen können statt immer switch/case.
        /// </summary>
        private void InitializeReferences()
        {
            _icons = new[] { Icon0, Icon1, Icon2, Icon3, Icon4 };
            _labels = new[] { Label0, Label1, Label2, Label3, Label4 };
            _glows = new[] { Glow0, Glow1, Glow2, Glow3, Glow4 };
        }

        // ==========================================================
        // Tap-Handler (XAML → Code-Behind)
        // ==========================================================

        private void OnTab0Tapped(object sender, TappedEventArgs e) => OnTabTapped(0);
        private void OnTab1Tapped(object sender, TappedEventArgs e) => OnTabTapped(1);
        private void OnTab2Tapped(object sender, TappedEventArgs e) => OnTabTapped(2);
        private void OnTab3Tapped(object sender, TappedEventArgs e) => OnTabTapped(3);
        private void OnTab4Tapped(object sender, TappedEventArgs e) => OnTabTapped(4);

        private async void OnTabTapped(int index)
        {
            if (_isAnimating || index == _currentIndex)
                return;

            // Haptic Feedback
#if IOS
            iOSHapticFeedback.TriggerHeavyImpact();
#endif
            HapticFeedback.Default.Perform(HapticFeedbackType.Click);

            // Tab visuell auswählen
            SelectTabInternal(index, animate: true);

            // Shell-Navigation auslösen
            try
            {
                if (index >= 0 && index < TabRoutes.Length)
                {
                    await Shell.Current.GoToAsync(TabRoutes[index]);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[CustomTabBar] Navigation fehlgeschlagen: {ex.Message}");
            }
        }

        // ==========================================================
        // Kern-Logik: Tab auswählen + animieren
        // ==========================================================

        private async void SelectTabInternal(int newIndex, bool animate)
        {
            if (_icons == null || newIndex < 0 || newIndex >= 5)
                return;

            int oldIndex = _currentIndex;
            _currentIndex = newIndex;

            var activeColor = TabColors[newIndex];

            // -------------------------------------------------------
            // 1. SLIDER positionieren
            // -------------------------------------------------------
            double sliderTargetX = CalculateSliderX(newIndex);

            if (animate && oldIndex >= 0)
            {
                _isAnimating = true;

                // Slider gleiten lassen (Spring-Easing = federnder Effekt)
                var sliderAnim = Slider.TranslateTo(
                    sliderTargetX, 0,
                    AnimationDuration,
                    Easing.SpringOut);

                // Dot synchron mitbewegen
                double dotTargetX = CalculateDotX(newIndex);
                var dotAnim = ActiveDot.TranslateTo(
                    dotTargetX, 0,
                    AnimationDuration,
                    Easing.SpringOut);

                // Alten Tab ausblenden (parallel)
                var oldFadeOut = AnimateTabDeactivate(oldIndex);

                // Neuen Tab einblenden (parallel)
                var newFadeIn = AnimateTabActivate(newIndex, activeColor);

                await Task.WhenAll(sliderAnim, dotAnim, oldFadeOut, newFadeIn);

                _isAnimating = false;
            }
            else
            {
                // Ohne Animation: sofort positionieren
                Slider.TranslationX = sliderTargetX;

                double dotTargetX = CalculateDotX(newIndex);
                ActiveDot.TranslationX = dotTargetX;

                // Alle Tabs zurücksetzen
                for (int i = 0; i < 5; i++)
                {
                    bool isActive = (i == newIndex);
                    _labels[i].TextColor = isActive ? activeColor : InactiveColor;
                    _glows[i].Opacity = isActive ? 0.7f : 0f;
                }
            }

            // -------------------------------------------------------
            // 2. SLIDER-Farbe an den aktiven Tab anpassen
            // -------------------------------------------------------
            UpdateSliderColor(activeColor);
            UpdateDotColor(activeColor);
        }

        // ==========================================================
        // Slider-Position berechnen
        // ==========================================================

        /// <summary>
        /// Berechnet die TranslationX des Sliders relativ zu seiner Startposition.
        /// Der Slider startet bei Margin.Left = 4 in Spalte 0.
        /// Jeder Tab hat die Breite: TabItemsGrid.Width / 5.
        /// </summary>
        private double CalculateSliderX(int tabIndex)
        {
            double gridWidth = TabItemsGrid.Width;

            // Falls Grid noch kein Layout hat, abbrechen
            if (gridWidth <= 0)
                return 0;

            double tabWidth = gridWidth / 5.0;
            double sliderWidth = tabWidth - 8;

            Slider.WidthRequest = sliderWidth;

            double targetCenter = (tabIndex * tabWidth) + (tabWidth / 2.0);
            double targetX = targetCenter - (sliderWidth / 2.0) - 4;

            return targetX;
        }
        /// <summary>
        /// Berechnet die TranslationX des Dots.
        /// </summary>
        private double CalculateDotX(int tabIndex)
        {
            double gridWidth = TabItemsGrid.Width;

            // Falls Grid noch kein Layout hat, abbrechen
            if (gridWidth <= 0)
                return 0;

            double tabWidth = gridWidth / 5.0;
            double targetCenter = (tabIndex * tabWidth) + (tabWidth / 2.0);

            return targetCenter - 2; // -2 = halbe Dot-Breite (4/2)
        }
        // ==========================================================
        // Tab aktivieren / deaktivieren (Animationen)
        // ==========================================================

        /// <summary>
        /// Animiert einen Tab in den aktiven Zustand:
        /// - Label-Farbe → Akzentfarbe
        /// - Glow-Opacity → 0.7
        /// - Icon kurz vergrößern (Bounce)
        /// </summary>
        private async Task AnimateTabActivate(int index, Color color)
        {
            var icon = _icons[index];
            var label = _labels[index];
            var glow = _glows[index];

            // Label-Farbe animiert wechseln
            label.TextColor = color;

            // Glow einblenden
            var glowAnim = new Animation(v => glow.Opacity = (float)v, 0, 0.7);
            glowAnim.Commit(this, $"GlowIn{index}", length: 300, easing: Easing.CubicOut);

            // Icon Bounce: vergrößern → zurück
            await icon.ScaleTo(1.25, 120, Easing.CubicOut);
            await icon.ScaleTo(1.0, 150, Easing.SpringOut);
        }

        /// <summary>
        /// Animiert einen Tab in den inaktiven Zustand:
        /// - Label-Farbe → InactiveColor
        /// - Glow-Opacity → 0
        /// </summary>
        private Task AnimateTabDeactivate(int index)
        {
            if (index < 0 || index >= 5) return Task.CompletedTask;

            var label = _labels[index];
            var glow = _glows[index];

            // Label zurücksetzen
            label.TextColor = InactiveColor;

            // Glow ausblenden
            var glowAnim = new Animation(v => glow.Opacity = (float)v, glow.Opacity, 0);
            glowAnim.Commit(this, $"GlowOut{index}", length: 200, easing: Easing.CubicIn);

            return Task.CompletedTask;
        }

        // ==========================================================
        // Farb-Updates für Slider und Dot
        // ==========================================================

        /// <summary>
        /// Aktualisiert die Slider-Hintergrundfarbe und den Stroke
        /// passend zum aktiven Tab.
        /// </summary>
        private void UpdateSliderColor(Color accentColor)
        {
            // Halbtransparente Version der Akzentfarbe für den Hintergrund
            var bgColor = accentColor.WithAlpha(0.12f);
            Slider.Background = new SolidColorBrush(bgColor);

            // Leicht sichtbarer Stroke
            var strokeColor = accentColor.WithAlpha(0.25f);
            Slider.Stroke = new SolidColorBrush(strokeColor);

            // Glow-Shadow unter dem Slider
            Slider.Shadow = new Shadow
            {
                Brush = new SolidColorBrush(accentColor),
                Opacity = 0.15f,
                Radius = 16,
                Offset = new Point(0, 2)
            };
        }

        /// <summary>
        /// Aktualisiert die Dot-Farbe und deren Glow.
        /// </summary>
        private void UpdateDotColor(Color accentColor)
        {
            ActiveDot.Color = accentColor;
            ActiveDot.Shadow = new Shadow
            {
                Brush = new SolidColorBrush(accentColor),
                Opacity = 0.8f,
                Radius = 4,
                Offset = new Point(0, 0)
            };
        }

        // ==========================================================
        // Öffentliche Methode: Tab von außen setzen (ohne Navigation)
        // Nützlich wenn die Seite per Shell.GoToAsync navigiert wird
        // und die TabBar nur visuell synchronisiert werden muss.
        // ==========================================================

        /// <summary>
        /// Setzt den aktiven Tab visuell, ohne Shell-Navigation auszulösen.
        /// Aufrufen in OnAppearing() jeder Tab-Seite:
        ///     MyTabBar.SetActiveTabWithoutNavigation(0);
        /// </summary>
        public void SetActiveTabWithoutNavigation(int index)
        {
            if (index == _currentIndex)
            {
                _ = RefreshUnreadChatBadgeAsync();
                return;
            }
            SelectTabInternal(index, animate: _isLayoutReady);
            _ = RefreshUnreadChatBadgeAsync();
        }

        /// <summary>
        /// Erkennt den aktiven Tab anhand der aktuellen Shell-Route.
        /// Aufrufen in OnAppearing():
        ///     MyTabBar.SyncWithCurrentRoute();
        /// </summary>
        public void SyncWithCurrentRoute()
        {
            var currentRoute = Shell.Current?.CurrentState?.Location?.ToString() ?? "";

            for (int i = 0; i < TabRoutes.Length; i++)
            {
                // Prüfe ob die aktuelle Route den Tab-Route-String enthält
                if (currentRoute.Contains(TabRoutes[i].TrimStart('/')))
                {
                    SetActiveTabWithoutNavigation(i);
                    return;
                }
            }

            // Fallback: Prüfe die Tab-Namen einzeln
            if (currentRoute.Contains("ProfilePage") || currentRoute.Contains("ProfileTab"))
                SetActiveTabWithoutNavigation(0);
            else if (currentRoute.Contains("MapPage") || currentRoute.Contains("MapTab"))
                SetActiveTabWithoutNavigation(1);
            else if (currentRoute.Contains("AddPage") || currentRoute.Contains("AddTab"))
                SetActiveTabWithoutNavigation(2);
            else if (currentRoute.Contains("ChatViewPage") || currentRoute.Contains("ChatTab"))
                SetActiveTabWithoutNavigation(3);
            else if (currentRoute.Contains("FAQPage") || currentRoute.Contains("FaqTab"))
                SetActiveTabWithoutNavigation(4);
        }

        public async Task RefreshUnreadChatBadgeAsync()
        {
            try
            {
                if (_supabaseClient == null || _authService == null)
                {
                    return;
                }

                var userId = _authService.GetCurrentUserId();
                if (string.IsNullOrWhiteSpace(userId) || !Guid.TryParse(userId, out var userGuid))
                {
                    ChatBadgeFrame.IsVisible = false;
                    return;
                }

                var unreadCount = await TryGetUnreadConversationCountAsync(userGuid);
                ChatBadgeLabel.Text = unreadCount > 99 ? "99+" : unreadCount.ToString();
                ChatBadgeFrame.IsVisible = unreadCount > 0;
            }
            catch
            {
                ChatBadgeFrame.IsVisible = false;
            }
        }

        private async Task<int> TryGetUnreadConversationCountAsync(Guid userGuid)
        {
            try
            {
                var response = await _supabaseClient!.Rpc("get_unread_conversation_count_self", new System.Collections.Generic.Dictionary<string, object>());
                if (response?.ResponseMessage?.IsSuccessStatusCode == true && !string.IsNullOrWhiteSpace(response.Content))
                {
                    var content = response.Content.Trim();
                    if (int.TryParse(content, out var count))
                    {
                        return count;
                    }

                    try
                    {
                        var token = Newtonsoft.Json.Linq.JToken.Parse(content);
                        if (token.Type == Newtonsoft.Json.Linq.JTokenType.Integer)
                        {
                            return token.ToObject<int>();
                        }

                        var countToken =
                            token["count"] ??
                            token["unread_count"] ??
                            token["get_unread_conversation_count_self"];

                        if (countToken != null && int.TryParse(countToken.ToString(), out var parsed))
                        {
                            return parsed;
                        }
                    }
                    catch
                    {
                        // keep fallback
                    }
                }
            }
            catch
            {
                // fallback
            }

            var fallbackResponse = await _supabaseClient!.Rpc("get_user_chats_safe", new System.Collections.Generic.Dictionary<string, object>
            {
                { "p_user_id", userGuid }
            });

            var chats = string.IsNullOrWhiteSpace(fallbackResponse?.Content)
                ? new System.Collections.Generic.List<ChatListRpcItem>()
                : JsonConvert.DeserializeObject<System.Collections.Generic.List<ChatListRpcItem>>(fallbackResponse.Content)
                  ?? new System.Collections.Generic.List<ChatListRpcItem>();

            return chats.Count(x => x.IsUnread);
        }
    }
}
