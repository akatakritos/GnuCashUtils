using System;
using System.Globalization;
using Avalonia.Controls.Converters;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace GnuCashUtils.Tagger;

public static class Converters
{
    public static readonly FuncValueConverter<bool, FontWeight> BoolToFontWeight =
        new(b => b ? FontWeight.Bold : FontWeight.Normal);

    public static readonly DateOnlyToDateTimeOffsetConverter DateOnlyToDateTimeOffset = new();
}

public class DateOnlyToDateTimeOffsetConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is DateOnly d ? new DateTimeOffset(d.ToDateTime(TimeOnly.MinValue)) : null;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is DateTimeOffset dto ? DateOnly.FromDateTime(dto.DateTime) : null;
}
