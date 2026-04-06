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

        // PopupOptions für transparenten Hintergrund
        private static readonly PopupOptions TransparentPopupOptions = new PopupOptions
        {
            PageOverlayColor = Colors.Transparent
        };

        public RegisterPage()
            : this(MauiProgram.Services.GetRequiredService<RegisterPageViewModel>(),
                  MauiProgram.Services)
        {
        }

        public RegisterPage(RegisterPageViewModel viewModel, IServiceProvider serviceProvider)
        {
            InitializeComponent();
            BindingContext = viewModel;
            _serviceProvider = serviceProvider;
        }

        // Events für Hilfe-Icons - NEUE SIGNATUR mit PopupOptions
        private async void OnUsernameHelpClicked(object sender, EventArgs e)
        {
            var popup = new HelpPopup("Der Benutzername, welcher repräsentativ für Sie verwendet wird.");
            await this.ShowPopupAsync(popup, TransparentPopupOptions);
        }

        private async void OnEmailHelpClicked(object sender, EventArgs e)
        {
            var popup = new HelpPopup("An diese Adresse wird eine Email gesendet, welche Sie bestätigen müssen, damit Ihr Account verifiziert ist.");
            await this.ShowPopupAsync(popup, TransparentPopupOptions);
        }
    }
}