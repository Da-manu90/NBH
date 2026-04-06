using NBH.Resources.ViewModels;
using NBH.Resources.Models;
using NBH.Resources.Views;
#if IOS
using NBH.Platforms.iOS;
#endif

namespace NBH
{
    public partial class ChatViewPage : ContentPage
    {
        // ------------------------------------------------------------
        // Konstruktor via DI
        // ------------------------------------------------------------
        public ChatViewPage(ChatViewPageViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
        }

        // ------------------------------------------------------------
        // OnAppearing => Liste neu laden
        // ------------------------------------------------------------
        protected override async void OnAppearing()
        {
            base.OnAppearing();

            try
            {
                // TabBar visuell auf diesen Tab setzen (ohne erneute Navigation)
                MyTabBar.SetActiveTabWithoutNavigation(3);  // 3 = Chat

                await MyBell.UpdateBadgeAsync();

                if (BindingContext is ChatViewPageViewModel vm)
                {
                    await vm.LoadChatsAsync();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ChatViewPage] OnAppearing Fehler: {ex.Message}");
            }
        }

        // ------------------------------------------------------------
        // SelectionChanged => Klick auf CollectionView
        // ------------------------------------------------------------
        private void OnChatSelected(object sender, SelectionChangedEventArgs e)
        {
            if (e.CurrentSelection == null || e.CurrentSelection.Count == 0)
                return;

            // Item vom Typ PublicChat
            var selectedChat = e.CurrentSelection[0] as PublicChat;
            if (selectedChat == null) return;
#if IOS
            iOSHapticFeedback.TriggerHeavyImpact();
#endif
            HapticFeedback.Perform(HapticFeedbackType.Click);

            // Debug-Ausgabe
            Console.WriteLine($"ChatViewPage: Chat ausgew�hlt: {selectedChat.Id}");

            // Navigation zur ChatPage
            Shell.Current.GoToAsync($"ChatPage?chatId={selectedChat.Id}");

            // Optional: Selektion aufheben
            ChatsCollectionView.SelectedItem = null;
        }
    }
}
