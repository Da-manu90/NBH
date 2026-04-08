using Mopups.Pages;
using Mopups.Services;
using NBH.Resources.Animations;

namespace NBH.Resources.Views
{
    public enum StatusBannerType
    {
        Success,
        Warning,
        Error,
        Info
    }

    public partial class StatusBannerPopup : PopupPage
    {
        private readonly TimeSpan _duration;
        private readonly TaskCompletionSource<bool> _dismissedTcs = new();
        private bool _isClosing;

        public StatusBannerPopup(StatusBannerType type, string message, TimeSpan duration)
        {
            InitializeComponent();
            _duration = duration;
            MessageLabel.Text = message;

            switch (type)
            {
                case StatusBannerType.Success:
                    BannerCard.BackgroundColor = Color.FromArgb("#1FA34A");
                    TitleLabel.Text = "Erfolg";
                    IconLabel.Text = "+";
                    break;
                case StatusBannerType.Warning:
                    BannerCard.BackgroundColor = Color.FromArgb("#F97316");
                    TitleLabel.Text = "Warnung";
                    IconLabel.Text = "!";
                    break;
                case StatusBannerType.Error:
                    BannerCard.BackgroundColor = Color.FromArgb("#D92D20");
                    TitleLabel.Text = "Fehler";
                    IconLabel.Text = "x";
                    break;
                default:
                    BannerCard.BackgroundColor = Color.FromArgb("#4B4B4B");
                    TitleLabel.Text = "Hinweis";
                    IconLabel.Text = "i";
                    break;
            }
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await AnimationHelper.SlideInFromBottomAsync(BannerCard);
            _ = AutoDismissAsync();
        }

        public Task WaitForDismissalAsync() => _dismissedTcs.Task;

        private async Task AutoDismissAsync()
        {
            await Task.Delay(_duration);
            await DismissAsync();
        }

        private async void OnDismissTapped(object? sender, TappedEventArgs e)
        {
            await DismissAsync();
        }

        private async Task DismissAsync()
        {
            if (_isClosing)
            {
                return;
            }

            _isClosing = true;

            try
            {
                await AnimationHelper.SlideOutToBottomAsync(BannerCard);
                if (MopupService.Instance.PopupStack.Contains(this))
                {
                    await MopupService.Instance.PopAsync();
                }
            }
            finally
            {
                _dismissedTcs.TrySetResult(true);
            }
        }
    }
}
