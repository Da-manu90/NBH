using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;

namespace NBH.Resources.Controls
{
    public partial class TypingIndicatorControl : ContentView
    {
        private CancellationTokenSource _animationCts;

        public TypingIndicatorControl()
        {
            InitializeComponent();
            PropertyChanged += OnIsVisibleChanged;
        }

        private void OnIsVisibleChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(IsVisible))
            {
                if (IsVisible)
                {
                    StartAnimation();
                }
                else
                {
                    StopAnimation();
                }
            }
        }

        private void StartAnimation()
        {
            StopAnimation(); // Sicherstellen, dass keine alte Animation läuft
            _animationCts = new CancellationTokenSource();
            var token = _animationCts.Token;

            Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        // Welle: 1, 2, 3 springen zeitversetzt
                        _ = AnimateDot(Dot1);
                        await Task.Delay(150);
                        _ = AnimateDot(Dot2);
                        await Task.Delay(150);
                        _ = AnimateDot(Dot3);
                        
                        await Task.Delay(800); // Pause vor der nächsten Welle
                    }
                    catch (OperationCanceledException) { break; }
                    catch { break; }
                }
            }, token);
        }

        private async Task AnimateDot(View dot)
        {
            if (dot == null) return;
            
            // Punkt springt hoch
            await dot.TranslateTo(0, -6, 250, Easing.CubicOut);
            // Punkt kommt zurück
            await dot.TranslateTo(0, 0, 250, Easing.CubicIn);
        }

        private void StopAnimation()
        {
            _animationCts?.Cancel();
            _animationCts = null;
            
            // Zurücksetzen der Positionen auf MainThread
            MainThread.BeginInvokeOnMainThread(() =>
            {
                Dot1.TranslationY = 0;
                Dot2.TranslationY = 0;
                Dot3.TranslationY = 0;
            });
        }
    }
}
