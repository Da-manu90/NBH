using NBH.Resources.ViewModels;
using Microsoft.Maui.Controls;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace NBH
{
    public partial class FAQPage : ContentPage
    {
        private readonly FAQPageViewModel _vm;
        private CancellationTokenSource? _loadingAnimCts;

        public FAQPage(FAQPageViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
            _vm = viewModel;
            _vm.PropertyChanged += OnViewModelPropertyChanged;
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            // Load FAQ when the page appears
            if (_vm != null && _vm.LoadFAQCommand.CanExecute(null))
            {
                _vm.LoadFAQCommand.Execute(null);
            }

            if (_vm.IsLoading)
            {
                StartLoadingAnimation();
            }
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            StopLoadingAnimation();
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(FAQPageViewModel.IsLoading))
            {
                return;
            }

            if (_vm.IsLoading)
            {
                StartLoadingAnimation();
            }
            else
            {
                StopLoadingAnimation();
            }
        }

        private void StartLoadingAnimation()
        {
            if (_loadingAnimCts != null)
            {
                return;
            }

            _loadingAnimCts = new CancellationTokenSource();
            _ = AnimateLoadingBarAsync(_loadingAnimCts.Token);
        }

        private void StopLoadingAnimation()
        {
            _loadingAnimCts?.Cancel();
            _loadingAnimCts = null;
            FaqLoadingProgressBar.Progress = 0;
        }

        private async Task AnimateLoadingBarAsync(CancellationToken token)
        {
            var bright = Color.FromArgb("#00E5FF");
            var dim = Color.FromArgb("#0A5A6E");
            try
            {
                while (!token.IsCancellationRequested)
                {
                    // Phase 1: Glow sweep left-to-right (bright, ~500ms)
                    FaqLoadingProgressBar.ProgressColor = bright;
                    await FaqLoadingProgressBar.ProgressTo(1, 500, Easing.SinIn);
                    if (token.IsCancellationRequested) break;

                    // Phase 2: Fade to dim and sweep back (darkening, ~500ms)
                    FaqLoadingProgressBar.ProgressColor = dim;
                    await FaqLoadingProgressBar.ProgressTo(0, 500, Easing.SinOut);
                    if (token.IsCancellationRequested) break;
                }
            }
            catch (TaskCanceledException)
            {
                // Expected while page is leaving/loading completed.
            }
        }

    }
}
