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

    // Deterministic palette (not GetHashCode — that's randomized per-process in .NET 5+)
    private static readonly Color[] _tagColors =
    [
        Color.FromRgb(0x19, 0x76, 0xD2), // blue
        Color.FromRgb(0x43, 0xA0, 0x47), // green
        Color.FromRgb(0xAD, 0x14, 0x57), // pink
        Color.FromRgb(0x6A, 0x1B, 0x9A), // purple
        Color.FromRgb(0xF5, 0x7C, 0x00), // amber
        Color.FromRgb(0x00, 0x83, 0x8F), // teal
        Color.FromRgb(0xC6, 0x28, 0x28), // red
        Color.FromRgb(0x37, 0x47, 0x4F), // blue-grey
    ];

    public static readonly FuncValueConverter<Tag?, IBrush> TagToColor = new(tag =>
    {
        if (tag == null) return new SolidColorBrush(Colors.Gray);
        var hash = tag.Name.Aggregate(0, (h, c) => h * 31 + c);
        return new SolidColorBrush(_tagColors[Math.Abs(hash) % _tagColors.Length]);
    });

    public static readonly FuncValueConverter<Tag?, string> TagToDisplayText = new(tag => tag switch
    {
        null => "",
        { Value: null } => tag.Name,
        _ => $"{tag.Name}={tag.Value}",
    });
}

public class DateOnlyToDateTimeOffsetConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is DateOnly d ? new DateTimeOffset(d.ToDateTime(TimeOnly.MinValue)) : null;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is DateTimeOffset dto ? DateOnly.FromDateTime(dto.DateTime) : null;
}
