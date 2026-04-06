using Microsoft.Maui.Controls;
using System;
using System.Diagnostics;
using NBH.Resources.ViewModels;

namespace NBH
{
    [QueryProperty(nameof(ReviewedUserId), "ReviewedUserId")]
    public partial class RatingPage : ContentPage
    {
        public string ReviewedUserId { get; set; }
        private readonly RatingPageViewModel _viewModel;

        public RatingPage(RatingPageViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = _viewModel;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await MyBell.UpdateBadgeAsync();

            Guid userGuid = Guid.Empty;
            if (!string.IsNullOrEmpty(ReviewedUserId))
            {
                Guid.TryParse(ReviewedUserId, out userGuid);
            }

            await _viewModel.LoadDataAsync(userGuid);
        }

        private async void OnBackToProfileClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("..");
        }
    }
}
