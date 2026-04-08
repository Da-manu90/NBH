using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Maui.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;
using NBH.Popups;
using NBH.Resources.ViewModels;

namespace NBH
{
    public partial class RegisterPage : ContentPage
    {
        private readonly IServiceProvider _serviceProvider;

        // PopupOptions fuer transparenten Hintergrund
        private static readonly PopupOptions TransparentPopupOptions = new PopupOptions
        {
            PageOverlayColor = Colors.Transparent
        };

        public RegisterPage()
            : this(((App)Application.Current!).GetServiceProvider().GetRequiredService<RegisterPageViewModel>(),
                  ((App)Application.Current!).GetServiceProvider())
        {
        }

        public RegisterPage(RegisterPageViewModel viewModel, IServiceProvider serviceProvider)
        {
            InitializeComponent();
            BindingContext = viewModel;
            _serviceProvider = serviceProvider;
        }

        // Events fuer Hilfe-Icons - neue Signatur mit PopupOptions
        private async void OnUsernameHelpClicked(object sender, EventArgs e)
        {
            var popup = new HelpPopup("Der Benutzername, der repraesentativ fuer dich verwendet wird.");
            await this.ShowPopupAsync(popup, TransparentPopupOptions);
        }

        private async void OnEmailHelpClicked(object sender, EventArgs e)
        {
            var popup = new HelpPopup("An diese Adresse wird eine E-Mail gesendet, die du bestaetigen musst.");
            await this.ShowPopupAsync(popup, TransparentPopupOptions);
        }
    }
}
