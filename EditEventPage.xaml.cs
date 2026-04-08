using NBH.Resources.ViewModels;
using System;
using System.ComponentModel;

namespace NBH;

[QueryProperty(nameof(EntryId), "entryId")]
public partial class EditEventPage : ContentPage
{
    private readonly EditEventPageViewModel _viewModel;

    public string? EntryId { get; set; }

    public EditEventPage(EditEventPageViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (Guid.TryParse(EntryId, out var entryGuid))
        {
            await _viewModel.InitializeAsync(entryGuid);
        }
    }

    private async void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(EditEventPageViewModel.IsBusy))
        {
            if (_viewModel.IsBusy)
            {
                await LoadingOverlay.ShowAsync("Event wird gespeichert...");
            }
            else
            {
                await LoadingOverlay.HideAsync();
            }
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
    }
}
