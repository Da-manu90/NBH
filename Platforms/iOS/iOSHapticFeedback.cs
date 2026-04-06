#if IOS
using UIKit;

namespace NBH.Platforms.iOS
{
    public static class iOSHapticFeedback
    {
        /// <summary>
        /// Löst ein Haptic-Feedback vom Typ "Error" aus
        /// (z. B. für Fehlermeldungen oder ungültige Aktionen).
        /// </summary>
        public static void TriggerErrorFeedback()
        {
            var feedbackGenerator = new UINotificationFeedbackGenerator();
            feedbackGenerator.Prepare();
            feedbackGenerator.NotificationOccurred(UINotificationFeedbackType.Error);
        }

        /// <summary>
        /// Löst ein kräftiges "Heavy"-Impact-Feedback aus
        /// (z. B. für gewichtige Bestätigungen oder sonstige starke Aktionen).
        /// </summary>
        public static void TriggerHeavyImpact()
        {
            var impactGenerator = new UIImpactFeedbackGenerator(UIImpactFeedbackStyle.Heavy);
            impactGenerator.Prepare();
            impactGenerator.ImpactOccurred();
        }

        /// <summary>
        /// Löst ein leichtes "Light"-Impact-Feedback aus
        /// (z. B. für Toggle-Switches, Filter-Änderungen, Expander).
        /// </summary>
        public static void TriggerLightImpact()
        {
            var generator = new UIImpactFeedbackGenerator(UIImpactFeedbackStyle.Light);
            generator.Prepare();
            generator.ImpactOccurred();
        }

        /// <summary>
        /// Löst ein mittleres "Medium"-Impact-Feedback aus.
        /// </summary>
        public static void TriggerMediumImpact()
        {
            var generator = new UIImpactFeedbackGenerator(UIImpactFeedbackStyle.Medium);
            generator.Prepare();
            generator.ImpactOccurred();
        }
    }
}
#endif