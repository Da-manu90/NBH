// ============================================================
// AnimationHelper.cs
// Legen in: Resources/Animations/AnimationHelper.cs
//
// Zentraler Baukasten für alle Animationen in der App.
// Einmal einbinden, überall verwenden.
// ============================================================

namespace NBH.Resources.Animations
{
    public static class AnimationHelper
    {
        // --------------------------------------------------------
        // SEITEN-EINGANG: Elemente von unten + Fade hereingleiten
        // Aufruf in OnAppearing():
        //   await AnimationHelper.PageEntranceAsync(MyCard, MyButton);
        // --------------------------------------------------------
        public static async Task PageEntranceAsync(
            params VisualElement[] elements)
        {
            // Alle unsichtbar und 40px nach unten versetzt starten
            foreach (var el in elements)
            {
                if (el == null) continue;
                el.Opacity      = 0;
                el.TranslationY = 40;
            }

            // Gestaffelt hereingleiten – jedes Element 80ms nach dem vorherigen
            foreach (var el in elements)
            {
                if (el == null) continue;

                // Explizit auf dem Main Thread ausführen
                _ = MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await Task.WhenAll(
                        el.FadeTo(1, 350, Easing.CubicOut),
                        el.TranslateTo(0, 0, 350, Easing.CubicOut)
                    );
                });

                await Task.Delay(80);
            }
        }

        // --------------------------------------------------------
        // BUTTON-PRESS: Leichtes Einfedern beim Tippen
        // Aufruf im TapGestureRecognizer Tapped-Handler:
        //   await AnimationHelper.ButtonPressAsync(MyFrame);
        // --------------------------------------------------------
        public static async Task ButtonPressAsync(
            VisualElement element,
            double scaleTo = 0.95)
        {
            await element.ScaleTo(scaleTo, 80, Easing.CubicIn);
            await element.ScaleTo(1.0,    120, Easing.SpringOut);
        }

        // --------------------------------------------------------
        // FEHLER-SCHÜTTEL: Horizontales Wackeln bei Validierungsfehler
        // Aufruf wenn Eingabe falsch:
        //   await AnimationHelper.ShakeAsync(EmailEntry);
        // --------------------------------------------------------
        public static async Task ShakeAsync(VisualElement element)
        {
            var originalX = element.TranslationX;
            var shakeSequence = new[] { 8, -8, 6, -6, 3, -3, 0 };

            foreach (var offset in shakeSequence)
            {
                await element.TranslateTo(originalX + offset, 0, 40, Easing.Linear);
            }
        }

        // --------------------------------------------------------
        // ERFOLG-PULS: Kurzes Aufleuchten bei Erfolg
        //   await AnimationHelper.PulseSuccessAsync(ConfirmButton);
        // --------------------------------------------------------
        public static async Task PulseSuccessAsync(VisualElement element)
        {
            await element.ScaleTo(1.08, 120, Easing.SpringOut);
            await element.ScaleTo(1.0,  200, Easing.SpringIn);
        }

        // --------------------------------------------------------
        // KARTEN-LISTE: Gestaffelte Einblendung einer Liste
        // Aufruf nach dem Laden der Daten:
        //   await AnimationHelper.StaggerListAsync(MyStackLayout.Children);
        // --------------------------------------------------------
        public static async Task StaggerListAsync(
            IEnumerable<IView> items,
            int delayBetween = 60)
        {
            var elements = items
                .OfType<VisualElement>()
                .ToList();

            // Alle vorbereiten
            foreach (var el in elements)
            {
                el.Opacity      = 0;
                el.TranslationY = 25;
            }

            // Gestaffelt einblenden
            foreach (var el in elements)
            {
                var localEl = el;
                _ = MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await Task.WhenAll(
                        localEl.FadeTo(1, 300, Easing.CubicOut),
                        localEl.TranslateTo(0, 0, 300, Easing.CubicOut)
                    );
                });
                await Task.Delay(delayBetween);
            }
        }

        // --------------------------------------------------------
        // CHAT-BUBBLE: Nachricht von der Seite hereingleiten
        //   await AnimationHelper.ChatBubbleEnterAsync(bubble, isOwn);
        // --------------------------------------------------------
        public static async Task ChatBubbleEnterAsync(
            VisualElement bubble,
            bool isOwnMessage)
        {
            bubble.Opacity      = 0;
            bubble.TranslationX = isOwnMessage ? 30 : -30;

            await Task.WhenAll(
                bubble.FadeTo(1, 200, Easing.CubicOut),
                bubble.TranslateTo(0, 0, 200, Easing.CubicOut)
            );
        }

        // --------------------------------------------------------
        // LOADING-PULS: Endlos-Pulsieren während etwas lädt
        //   var cts = AnimationHelper.StartPulse(LoadingSpinner);
        //   // ... laden ...
        //   cts.Cancel();
        // --------------------------------------------------------
        public static CancellationTokenSource StartPulse(VisualElement element)
        {
            var cts = new CancellationTokenSource();
            var token = cts.Token;

            Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    await MainThread.InvokeOnMainThreadAsync(async () =>
                    {
                        await element.FadeTo(0.3, 600, Easing.SinInOut);
                        if (!token.IsCancellationRequested)
                            await element.FadeTo(1.0, 600, Easing.SinInOut);
                    });
                }
            }, token);

            return cts;
        }

        // --------------------------------------------------------
        // MODAL SLIDE-IN: Ein Popup von unten hereinfahren
        //   await AnimationHelper.SlideInFromBottomAsync(PopupPanel);
        // --------------------------------------------------------
        public static async Task SlideInFromBottomAsync(VisualElement element)
        {
            element.TranslationY = 400;
            element.Opacity      = 0;
            element.IsVisible    = true;

            await Task.WhenAll(
                element.TranslateTo(0, 0, 350, Easing.CubicOut),
                element.FadeTo(1,       250, Easing.CubicOut)
            );
        }

        public static async Task SlideOutToBottomAsync(VisualElement element)
        {
            await Task.WhenAll(
                element.TranslateTo(0, 400, 300, Easing.CubicIn),
                element.FadeTo(0,          200, Easing.CubicIn)
            );
            element.IsVisible = false;
        }
    }
}
