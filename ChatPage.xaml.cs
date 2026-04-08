using Microsoft.Maui.Controls;
using NBH.Resources.ViewModels;
using NBH.Resources.Models; // Für ChatMessageDataTemplateSelector
using System;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
#if IOS
using NBH.Platforms.iOS;
#endif


namespace NBH
{
    [QueryProperty(nameof(ChatIdString), "chatId")]
    public partial class ChatPage : ContentPage
    {
        private ChatPageViewModel ViewModel => (ChatPageViewModel)BindingContext;

        public string ChatIdString
        {
            get => null;
            set
            {
                Console.WriteLine($"ChatPage: ChatIdString erhalten = {value}");
                if (Guid.TryParse(value, out var guid))
                {
                    ViewModel.ChatId = guid;
                }
                else
                {
                    Console.WriteLine("ChatPage: Konnte chatId nicht in Guid parsen!");
                }
            }
        }

        public ChatPage(ChatPageViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;

            // TemplateSelector holen & "CurrentUserId" setzen
            if (Resources.TryGetValue("ChatMessageDataTemplateSelector", out var selectorObj)
                && selectorObj is ChatMessageDataTemplateSelector selector)
            {
                selector.CurrentUserId = viewModel.CurrentUserId;
            }

            // Realtime Listener für Nachrichten
            ViewModel.Messages.CollectionChanged += Messages_CollectionChanged;

            // Tastaturen konfigurieren (Beispiel)
            
            MessageEntry.Focused += OnMessageEntryFocused;
            MessageEntry.Unfocused += OnMessageEntryUnfocused;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            Console.WriteLine("ChatPage OnAppearing => Realtime Listener aktiv");

            try
            {
                // ♻️ Chat als gelesen markieren
                await ViewModel.MarkChatAsReadAsync();

                // Warte kurz auf ChatPartnerId
                for (int i = 0; i < 5; i++)
                {
                    if (ViewModel.ChatPartnerId != Guid.Empty)
                        break;
                    await Task.Delay(200);
                }

                if (!ViewModel.IsGroupConversation && ViewModel.ChatId != Guid.Empty && ViewModel.ChatPartnerId != Guid.Empty)
                {
                    await ViewModel.CheckForReviewPopup(ViewModel.ChatId, ViewModel.ChatPartnerId);
                }

                // Nach dem Laden automatisch ans Ende scrollen
                await Task.Delay(150);
                ScrollToLastMessage();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ChatPage] OnAppearing Fehler: {ex.Message}");
            }
        }

        private void ScrollToLastMessage()
        {
            Dispatcher.Dispatch(async () =>
            {
                if (ViewModel?.Messages?.Count > 0)
                {
                    // Kleiner Delay, damit CollectionView das Layout fertig hat
                    await Task.Delay(100);
                    var lastMessage = ViewModel.Messages.LastOrDefault();
                    if (lastMessage != null)
                    {
                        MessagesCollectionView.ScrollTo(
                            lastMessage,
                            position: ScrollToPosition.End,
                            animate: false);
                    }
                }
            });
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            Console.WriteLine("ChatPage OnDisappearing => Realtime Listener deaktivieren");
            ViewModel.Messages.CollectionChanged -= Messages_CollectionChanged;
            MessageEntry.Focused -= OnMessageEntryFocused;
            MessageEntry.Unfocused -= OnMessageEntryUnfocused;
            ViewModel.Cleanup();
        }

        private void Messages_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add && ViewModel.Messages.Count > 0)
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await Task.Delay(100);
                    var lastMessage = ViewModel.Messages.LastOrDefault();
                    if (lastMessage != null)
                    {
                        MessagesCollectionView.ScrollTo(
                            lastMessage,
                            position: ScrollToPosition.End,
                            animate: true);
                    }
                });
            }
        }

        // -----------------------------------------------------------
        // Senden-Button: Bounce-Animation + Haptic + Command
        // -----------------------------------------------------------
        private async void OnSendButtonTapped(object sender, TappedEventArgs e)
        {
            try
            {
                // 1) Bounce-Animation
                if (sender is VisualElement element)
                {
                    await element.ScaleTo(0.8, 80, Easing.CubicIn);
                    await element.ScaleTo(1.1, 80, Easing.CubicOut);
                    await element.ScaleTo(1.0, 50, Easing.Linear);
                }

                // 2) Haptic Feedback
#if IOS
                iOSHapticFeedback.TriggerHeavyImpact();
#endif
                HapticFeedback.Perform(HapticFeedbackType.Click);

                // 3) Nachricht im ViewModel absenden
                await ViewModel.SendMessageAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ChatPage] OnSendButtonTapped Fehler: {ex.Message}");
            }
        }

        private void OnBackgroundTapped(object sender, EventArgs e)
        {
            if (MessageEntry.IsFocused)
            {
                MessageEntry.Unfocus();
            }
        }

        private void OnMessageEntryFocused(object sender, FocusEventArgs e)
        {
            // Optional: Scroll zum Ende, wenn die Tastatur erscheint
            ScrollToLastMessage();
        }

        private void OnMessageEntryUnfocused(object sender, FocusEventArgs e)
        {
            // Optional: Logik beim Verlassen des Textfeldes
        }
    }
}
