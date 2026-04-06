using CommunityToolkit.Maui.Views;

namespace NBH.Popups;

public partial class HelpPopup : Popup
{
    public string HelpText { get; set; }

    public HelpPopup(string helpText)
    {
        InitializeComponent();
        HelpText = helpText;
        BindingContext = this;

        // ❌ ENTFERNT - Color Property existiert nicht mehr in Popup v2
        // this.Color = Colors.Transparent;
    }
}