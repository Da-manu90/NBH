using Microsoft.Maui.Devices;

public static class HapticFeedbackHelper
{
    public static void PerformClickFeedback()
    {
        HapticFeedback.Default.Perform(HapticFeedbackType.Click);
    }

    public static void PerformErrorFeedback()
    {
        HapticFeedback.Default.Perform(HapticFeedbackType.LongPress);
    }
}