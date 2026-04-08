using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using System;
using System.Threading.Tasks;
using NBH.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
#if IOS
using NBH.Platforms.iOS;
#endif

namespace NBH
{
    public partial class SplashPage : ContentPage
    {
        private readonly AppSessionFlowService _appSessionFlowService;

        public SplashPage(AppSessionFlowService appSessionFlowService)
        {
            InitializeComponent();
            _appSessionFlowService = appSessionFlowService;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            try
            {
                var startupPreparationTask = _appSessionFlowService.PrepareStartupAsync();
                await RunLogoAnimationAsync();
                await RunSessionCheckAsync(startupPreparationTask);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SplashPage] Fehler: {ex.Message}");
                await RunSessionCheckAsync(null);
            }
        }

        // ----------------------------------------------------------------
        // Animationssequenz
        // ----------------------------------------------------------------
        private async Task RunLogoAnimationAsync()
        {
            // ── Ausgangszustand ───────────────────────────────────────
            logo.Opacity      = 0;
            logo.Scale        = 0.25;
            logo.TranslationY = 220;
            logo.Rotation     = -6;

            GlowOuter.Opacity = 0;
            GlowOuter.Scale   = 0.4;
            GlowInner.Opacity = 0;
            GlowInner.Scale   = 0.5;

            RippleRing1.Opacity = 0;
            RippleRing1.Scale   = 1.0;
            RippleRing2.Opacity = 0;
            RippleRing2.Scale   = 1.0;

            TaglineLabel.Opacity      = 0;
            TaglineLabel.TranslationY = 170; // Startet tiefer für einen schöneren Aufstieg
            LoadingLabel.Opacity      = 0;

            // ── Phase 1: Logo schwebt von unten herein ─────────────────
            // Gleichzeitig: Fade-in, Aufstieg, Vergrößerung, Geraderichten
            await Task.WhenAll(
                logo.FadeTo(1,            650, Easing.CubicOut),
                logo.TranslateTo(0, 0,    650, Easing.CubicOut),
                logo.ScaleTo(1.18,        650, Easing.CubicOut),
                logo.RotateTo(0,          500, Easing.CubicOut)
            );

            // ── Phase 2: Spring-Bounce (echte Federmechanik) ──────────
            // Komprimiert → schwingt auf → kleiner Bounce → ausklingen
            await logo.ScaleTo(0.86, 160, Easing.CubicIn);    // Aufprall – komprimiert
            await logo.ScaleTo(1.13, 230, Easing.CubicOut);   // Federt weit auf

            // Ripple-Welle 1 auslösen (fire & forget)
            _ = FireRippleAsync(RippleRing1, 600, 2.4);

            await logo.ScaleTo(0.93, 150, Easing.CubicIn);    // 2. Kompression
            await logo.ScaleTo(1.06, 180, Easing.CubicOut);   // 2. Bounce

            // Ripple-Welle 2 (Echo, etwas verzögert und schwächer)
            _ = FireRippleAsync(RippleRing2, 700, 2.0);

            await logo.ScaleTo(0.97, 110, Easing.CubicIn);    // 3. Kompression
            await logo.ScaleTo(1.02, 130, Easing.CubicOut);   // 3. Bounce (klein)
            await logo.ScaleTo(0.99, 80,  Easing.CubicIn);    // Feines Ausklingen
            await logo.ScaleTo(1.00, 100, Easing.CubicOut);   // Endstellung: exakt 1.0

            // ── Phase 3: Glow-Ringe erscheinen ────────────────────────
            await Task.WhenAll(
                GlowOuter.FadeTo(0.85, 500, Easing.CubicOut),
                GlowOuter.ScaleTo(1.0,  500, Easing.CubicOut),
                GlowInner.FadeTo(1.0,   400, Easing.CubicOut),
                GlowInner.ScaleTo(1.0,  400, Easing.CubicOut)
            );

            // ── Phase 4: Tagline gleitet von unten herauf ─────────────
            await Task.WhenAll(
                TaglineLabel.FadeTo(1,             700, Easing.CubicOut),
                TaglineLabel.TranslateTo(0, 125,   700, Easing.CubicOut)
            );

            // ── Phase 5: Lade-Text erscheint (während Session-Prüfung) ─
            await LoadingLabel.FadeTo(0.6, 400, Easing.CubicOut);

            // Kurze Pause bevor Navigation
            await Task.Delay(300);
        }

        // ----------------------------------------------------------------
        // Ripple-Effekt: Ring expandiert und verblasst
        // ----------------------------------------------------------------
        private async Task FireRippleAsync(Ellipse ring, uint duration, double targetScale)
        {
            ring.Scale   = 1.0;
            ring.Opacity = 0.75;

            await Task.WhenAll(
                ring.FadeTo(0,                  duration, Easing.CubicOut),
                ring.ScaleTo(targetScale,        duration, Easing.CubicOut)
            );

            ring.Opacity = 0;
            ring.Scale   = 1.0;
        }

        // ----------------------------------------------------------------
        // Session prüfen und navigieren
        // ----------------------------------------------------------------
        private async Task RunSessionCheckAsync(Task<bool>? startupPreparationTask)
        {
            // Lade-Label ausblenden
            await LoadingLabel.FadeTo(0, 200, Easing.CubicIn);

            bool sessionValid = startupPreparationTask != null
                ? await startupPreparationTask
                : await _appSessionFlowService.PrepareStartupAsync();

            sessionValid = await _appSessionFlowService.CompleteStartupAsync(
                sessionValid,
                navigateToLastPage: false,
                showSessionExpiredToast: true);

            if (!sessionValid)
            {
#if IOS
                iOSHapticFeedback.TriggerHeavyImpact();
#endif
                HapticFeedback.Perform(HapticFeedbackType.Click);
            }
        }
    }
}
