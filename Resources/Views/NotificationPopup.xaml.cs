using Mopups.Pages;
using Mopups.Services;
using NBH.Resources.ViewModels;
using System.Diagnostics;

namespace NBH.Resources.Views
{
    public partial class NotificationPopup : PopupPage
    {
        private readonly NotificationListViewModel _viewModel;

        public NotificationPopup(NotificationListViewModel vm)
        {
            InitializeComponent();
            _viewModel = vm;
            BindingContext = vm;
            Debug.WriteLine($"[NotificationPopup] BindingContext gesetzt. Notifications Count: {vm.Notifications.Count}");
        }

        private async void OnCloseButtonTapped(object sender, EventArgs e)
        {
            await MopupService.Instance.PopAsync();
        }
        
        protected override async void OnAppearing()
        {
            base.OnAppearing();
            if (_viewModel.RefreshCommand.CanExecute(null))
            {
                _viewModel.RefreshCommand.Execute(null);
            }
        }

    }
}