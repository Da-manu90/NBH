using System.Globalization;

namespace NBH.Resources.Models;

public class NotificationIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool isRead = (bool)value;
        // "notification_unread.png" vs. "Notifyclose.png" 
        return isRead ? "notification_read.png" : "notification_unread.png";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
