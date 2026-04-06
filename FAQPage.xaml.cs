using NBH.Resources.ViewModels;
using Microsoft.Maui.Controls;

namespace NBH
{
    public partial class FAQPage : ContentPage
    {
        private readonly FAQPageViewModel _vm;

        public FAQPage(FAQPageViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
            _vm = viewModel;
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            // Load FAQ when the page appears
            if (_vm != null && _vm.LoadFAQCommand.CanExecute(null))
            {
                _vm.LoadFAQCommand.Execute(null);
            }
        }
    }
}
