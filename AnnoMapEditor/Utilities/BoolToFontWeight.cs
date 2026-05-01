using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace AnnoMapEditor.Utilities
{
    /// <summary>True → SemiBold, False → Normal. Used to highlight tree categories vs leaves.</summary>
    public class BoolToFontWeight : IValueConverter
    {
        public static readonly BoolToFontWeight Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => (value is bool b && b) ? FontWeight.SemiBold : FontWeight.Normal;

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
