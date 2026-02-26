using System;
using System.Globalization;
using System.Linq;
using Avalonia.Controls.Converters;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace GnuCashUtils.Tagger;

public static class Converters
{
    public static readonly FuncValueConverter<bool, FontWeight> BoolToFontWeight =
        new(b => b ? FontWeight.Bold : FontWeight.Normal);

    public static readonly DateOnlyToDateTimeOffsetConverter DateOnlyToDateTimeOffset = new();

    public static readonly FuncValueConverter<OperationType, double> OperationToOpacity =
        new(op => op == OperationType.None ? 0.75 : 1.0);

    public static readonly FuncValueConverter<OperationType, bool> OperationIsNotNone =
        new(op => op != OperationType.None);

    public static readonly FuncValueConverter<OperationType, string> OperationToIcon =
        new(op => op switch
        {
            OperationType.Add => "+",
            OperationType.Delete => "−",
            _ => ""
        });

    // Green #2E7D32 (Material green 800), Red #C62828 (Material red 800).
    // The badge also gets a white border so it reads clearly against any chip background.
    public static readonly FuncValueConverter<OperationType, IBrush> OperationToBadgeBackground =
        new(op => op == OperationType.Delete
            ? new SolidColorBrush(Color.FromRgb(0xC6, 0x28, 0x28))
            : new SolidColorBrush(Color.FromRgb(0x2E, 0x7D, 0x32)));

    // Hides the ContentPresenter when TrailingContent was never set (null).
    public static readonly FuncValueConverter<object?, bool> IsNotNull = new(x => x is not null);
}

public class DateOnlyToDateTimeOffsetConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is DateOnly d ? new DateTimeOffset(d.ToDateTime(TimeOnly.MinValue)) : null;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is DateTimeOffset dto ? DateOnly.FromDateTime(dto.DateTime) : null;
}
