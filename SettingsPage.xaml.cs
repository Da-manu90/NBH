using Microsoft.Maui.Controls;
using NBH.Resources.ViewModels;
using System.ComponentModel;

namespace NBH
{
    public partial class SettingsPage : ContentPage
    {
        private SettingsPageViewModel _viewModel;

        public SettingsPage(SettingsPageViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = viewModel;

            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        private async void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SettingsPageViewModel.IsBusy))
            {
                if (_viewModel.IsBusy)
                    await LoadingOverlay.ShowAsync("Account wird gelöscht...");
                else
                    await LoadingOverlay.HideAsync();
            }
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }
    }
}
