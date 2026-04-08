using NBH.Resources.ViewModels;

namespace NBH;

[QueryProperty(nameof(InitialSection), "section")]
public partial class MyEventsPage : ContentPage
{
    private readonly MyEventsPageViewModel _viewModel;
    private bool _initialized;

    public string? InitialSection { get; set; }

    public MyEventsPage(MyEventsPageViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        await _viewModel.InitializeAsync(InitialSection);
    }

    private async void OnBackTapped(object sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }
}
