using Avalonia;
using Avalonia.Controls;

namespace GnuCashUtils.Tagger;

public partial class TagChip : UserControl
{
    public static readonly StyledProperty<Tag?> ChipTagProperty =
        AvaloniaProperty.Register<TagChip, Tag?>(nameof(ChipTag));

    public Tag? ChipTag
    {
        get => GetValue(ChipTagProperty);
        set => SetValue(ChipTagProperty, value);
    }

    public TagChip() => InitializeComponent();
}
