// ============================================================
// EntryCard.xaml.cs
// Legen in: Resources/Controls/EntryCard.xaml.cs
//
// Code-Behind für die polished EntryCard.
// Setzt die Akzentfarbe je nach Kategorie und
// führt eine Press-Animation beim Tippen aus.
// ============================================================

using NBH.Resources.Animations;
using NBH.Resources.Models;
using Microsoft.Maui.Devices;
#if IOS
using NBH.Platforms.iOS;
#endif

namespace NBH.Resources.Controls
{
    public partial class EntryCard : ContentView
    {
        public EntryCard()
        {
            InitializeComponent();
        }

        // --------------------------------------------------------
        // Akzentfarbe je nach Kategorie setzen
        // --------------------------------------------------------
        protected override void OnBindingContextChanged()
        {
            base.OnBindingContextChanged();

            if (BindingContext is EntryItem entry)
            {
                // Anfrage = Türkis, Angebot = Warmes Orange
                bool isRequest = entry.category?.ToLower() == "anfrage";

                AccentBar.BackgroundColor = isRequest
                    ? Color.FromArgb("#00897b")
                    : Color.FromArgb("#e67e22");

                CategoryLabel.TextColor = isRequest
                    ? Color.FromArgb("#00897b")
                    : Color.FromArgb("#e67e22");
            }
        }

        // --------------------------------------------------------
        // Press-Animation + Navigation via ViewModel
        // --------------------------------------------------------
        private async void OnCardTapped(object sender, TappedEventArgs e)
        {
            // Haptic Feedback
#if IOS
            iOSHapticFeedback.TriggerHeavyImpact();
#endif
            HapticFeedback.Perform(HapticFeedbackType.Click);

            // Karte kurz einfedern lassen
            await Task.WhenAll(
                CardRoot.ScaleTo(0.97, 80, Easing.CubicIn),
                CardRoot.FadeTo(0.85, 80)
            );
            await Task.WhenAll(
                CardRoot.ScaleTo(1.0, 150, Easing.SpringOut),
                CardRoot.FadeTo(1.0, 150)
            );

            // OpenDetailCommand aus dem AddPageViewModel aufrufen
            if (BindingContext is EntryItem entry)
            {
                var page = GetParentPage();
                if (page?.BindingContext is NBH.Resources.ViewModels.AddPageViewModel vm)
                {
                    vm.OpenDetailCommand?.Execute(entry);
                }
            }
        }

        // --------------------------------------------------------
        // Hilfsmethode: Parent-Page finden
        // --------------------------------------------------------
        private Page GetParentPage()
        {
            Element current = this;
            while (current != null)
            {
                if (current is Page page)
                    return page;
                current = current.Parent;
            }
            return null;
        }
    }
}
