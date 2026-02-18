using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace GnuCashUtils.Categorization;

public class ConfidenceToColorConverter : IValueConverter
{
    public static readonly ConfidenceToColorConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not double confidence) return Brushes.Transparent;

        confidence = Math.Clamp(confidence, 0, 1);

        // Pastel red (0%) → pastel yellow (50%) → pastel green (100%)
        Color color;
        if (confidence <= 0.5)
        {
            var t = confidence * 2; // 0→1
            color = Color.FromRgb(255, (byte)(200 + 55 * t), 200);
        }
        else
        {
            var t = (confidence - 0.5) * 2; // 0→1
            color = Color.FromRgb((byte)(255 - 55 * t), 255, 200);
        }

        return new SolidColorBrush(color);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class ValidToRowBrushConverter : IValueConverter
{
    public static readonly ValidToRowBrushConverter Instance = new();
    private static readonly SolidColorBrush ValidBrush = new(Color.FromRgb(235, 235, 235));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? ValidBrush : Brushes.Transparent;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
