using NBH.Resources.ViewModels;
using System.ComponentModel;

namespace NBH;

public partial class CreateEventPage : ContentPage
{
    private CreateEventPageViewModel _viewModel;

    public CreateEventPage(CreateEventPageViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;

        // LoadingOverlay beim IsBusy-Property-Change an/ausblenden
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private async void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CreateEventPageViewModel.IsBusy))
        {
            if (_viewModel.IsBusy)
                await LoadingOverlay.ShowAsync("Event wird erstellt...");
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
