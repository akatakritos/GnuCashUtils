using Avalonia.Controls.Converters;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace GnuCashUtils.Tagger;

public static class Converters
{
    public static readonly FuncValueConverter<bool, FontWeight> BoolToFontWeight =
        new(b => b ? FontWeight.Bold : FontWeight.Normal);
}
