using System.Globalization;
using Avalonia.Media;
using AwesomeAssertions;
using GnuCashUtils.Core;
using GnuCashUtils.Core.Controls;
using GnuCashUtils.Tagger;

namespace GnuCashUtils.Tests.Core.Controls;

public class TaggerConverterTests
{
    // TagToDisplayText

    [Fact]
    public void TagToDisplayText_NameOnly_ReturnsName()
    {
        var result = TagConverters.TagToDisplayText.Convert(new Tag("vacation"), typeof(string), null, CultureInfo.InvariantCulture);
        result.Should().Be("vacation");
    }

    [Fact]
    public void TagToDisplayText_NameAndValue_ReturnsNameEqualsValue()
    {
        var result = TagConverters.TagToDisplayText.Convert(new Tag("vacation", "disney-2024"), typeof(string), null, CultureInfo.InvariantCulture);
        result.Should().Be("vacation=disney-2024");
    }

    [Fact]
    public void TagToDisplayText_Null_ReturnsEmpty()
    {
        var result = TagConverters.TagToDisplayText.Convert(null, typeof(string), null, CultureInfo.InvariantCulture);
        result.Should().Be("");
    }

    // TagToColor

    [Fact]
    public void TagToColor_SameTag_ReturnsSameColor()
    {
        var a = TagConverters.TagToColor.Convert(new Tag("food"), typeof(IBrush), null, CultureInfo.InvariantCulture) as SolidColorBrush;
        var b = TagConverters.TagToColor.Convert(new Tag("food"), typeof(IBrush), null, CultureInfo.InvariantCulture) as SolidColorBrush;
        a.Should().NotBeNull();
        a!.Color.Should().Be(b!.Color);
    }

    [Fact]
    public void TagToColor_NullTag_ReturnsGray()
    {
        var result = TagConverters.TagToColor.Convert(null, typeof(IBrush), null, CultureInfo.InvariantCulture) as SolidColorBrush;
        result.Should().NotBeNull();
        result!.Color.Should().Be(Colors.Gray);
    }

    [Fact]
    public void TagToColor_DifferentTags_ReturnsDifferentColors()
    {
        var colors = new[] { "food", "travel", "vacation", "work", "health", "home", "auto", "misc" }
            .Select(n => (TagConverters.TagToColor.Convert(new Tag(n), typeof(IBrush), null, CultureInfo.InvariantCulture) as SolidColorBrush)!.Color)
            .Distinct()
            .Count();
        colors.Should().BeGreaterThan(1);
    }
}
