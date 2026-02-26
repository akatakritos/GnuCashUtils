using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace GnuCashUtils.Core.Controls;

public class IconButton : Button
{
    protected override Type StyleKeyOverride => typeof(Button);

    private static readonly FontFamily FontAwesome =
        new FontFamily("avares://GnuCashUtils/Assets/Fonts#Font Awesome 7 Free");

    public static readonly StyledProperty<string?> LeadingIconProperty =
        AvaloniaProperty.Register<IconButton, string?>(nameof(LeadingIcon));

    public static readonly StyledProperty<string?> TrailingIconProperty =
        AvaloniaProperty.Register<IconButton, string?>(nameof(TrailingIcon));

    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<IconButton, string?>(nameof(Text));

    public string? LeadingIcon
    {
        get => GetValue(LeadingIconProperty);
        set => SetValue(LeadingIconProperty, value);
    }

    public string? TrailingIcon
    {
        get => GetValue(TrailingIconProperty);
        set => SetValue(TrailingIconProperty, value);
    }

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LeadingIconProperty ||
            change.Property == TrailingIconProperty ||
            change.Property == TextProperty)
        {
            RebuildContent();
        }
    }

    private void RebuildContent()
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };

        if (LeadingIcon is { } leading)
            panel.Children.Add(new TextBlock
            {
                Text = leading,
                FontFamily = FontAwesome,
                FontWeight = FontWeight.Black,
                VerticalAlignment = VerticalAlignment.Center,
            });

        if (Text is { } text)
            panel.Children.Add(new TextBlock
            {
                Text = text,
                VerticalAlignment = VerticalAlignment.Center,
            });

        if (TrailingIcon is { } trailing)
            panel.Children.Add(new TextBlock
            {
                Text = trailing,
                FontFamily = FontAwesome,
                FontWeight = FontWeight.Black,
                VerticalAlignment = VerticalAlignment.Center,
            });

        Content = panel;
    }
}
