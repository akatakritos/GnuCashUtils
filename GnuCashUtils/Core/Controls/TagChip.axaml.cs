using Avalonia;
using Avalonia.Controls.Primitives;

namespace GnuCashUtils.Core.Controls;

public class TagChip : TemplatedControl
{
    public static readonly DirectProperty<TagChip, Tag?> ChipTagProperty =
        AvaloniaProperty.RegisterDirect<TagChip, Tag?>(
            nameof(ChipTag),
            o => o.ChipTag,
            (o, v) => o.ChipTag = v);

    private Tag? _chipTag;
    public Tag? ChipTag
    {
        get => _chipTag;
        set => SetAndRaise(ChipTagProperty, ref _chipTag, value);
    }

    public static readonly StyledProperty<object?> TrailingContentProperty =
        AvaloniaProperty.Register<TagChip, object?>(nameof(TrailingContent));

    public object? TrailingContent
    {
        get => GetValue(TrailingContentProperty);
        set => SetValue(TrailingContentProperty, value);
    }

    internal static Tag PreviewTag { get; } = new Tag("vaction");
    internal static Tag PreviewTagWithValues { get; } = new Tag("vacation", "disney");
}
