using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using NBH.Resources.ViewModels;
using NBH.Resources.Controls;
using NBH.Resources.Views;
using System;

namespace NBH
{
    public partial class UserProfilePage : ContentPage
    {
        private readonly UserProfilePageViewModel _viewModel;

        public UserProfilePage(UserProfilePageViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = _viewModel = viewModel;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await MyBell.UpdateBadgeAsync();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
        }
    }
}
