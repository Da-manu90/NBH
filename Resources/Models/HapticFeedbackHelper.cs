using Microsoft.Maui.Devices;

public static class HapticFeedbackHelper
{
    public static void PerformClickFeedback()
    {
        HapticFeedback.Default.Perform(HapticFeedbackType.Click);
    }

    public static void PerformSuccessFeedback()
    {
        HapticFeedback.Default.Perform(HapticFeedbackType.Click);
    }

    public static void PerformWarningFeedback()
    {
        HapticFeedback.Default.Perform(HapticFeedbackType.LongPress);
    }

    public static void PerformErrorFeedback()
    {
        HapticFeedback.Default.Perform(HapticFeedbackType.LongPress);
    }

    public static void PerformNotificationFeedback()
    {
        HapticFeedback.Default.Perform(HapticFeedbackType.Click);
    }
}
