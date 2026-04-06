using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace NBH.Resources.Models
{
    public class StringToVisibilityConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string strValue && !string.IsNullOrWhiteSpace(strValue))
            {
                return true; // Sichtbar, wenn nicht leer
            }

            return false; // Nicht sichtbar
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException(); // Optional: Rückwärtskonvertierung nicht unterstützt
        }
    }
}
