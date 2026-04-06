using Mopups.Pages;
using Mopups.Services;
using NBH.Services;
using NBH.Resources.Models;
using NBH.Resources.ViewModels;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
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

        private void OnCloseButtonTapped(object sender, EventArgs e)
        {
            MopupService.Instance.PopAsync();
        }
        
        protected override async void OnAppearing()
        {
            base.OnAppearing();

            // Alle ungelesenen auf read=true
            await _viewModel.MarkAllAsReadAsync();
        }

    }
}