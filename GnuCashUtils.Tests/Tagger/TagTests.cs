using AwesomeAssertions;
using GnuCashUtils.Tagger;

namespace GnuCashUtils.Tests.Tagger;

public class TagTests
{
    [Theory]
    [MemberData(nameof(ParseData))]
    public void Parse(string input, Tag result)
    {
        Tag parsed = Tag.Parse(input).Single();
        parsed.Should().Be(result);
    }

    public static IEnumerable<object[]> ParseData() =>
    [
        ["#[vacation]", new Tag("vacation")],
        ["#[vacation=disney]", new Tag("vacation", "disney")],
        [
            "#[business trip=kcdc conference]", new Tag("business trip", "kcdc conference")
        ], // spaces allowed in name and value
        ["preamble junk #[gift=susan] other crap", new Tag("gift", "susan")] // Handles other stuff in the memo
    ];

    [Theory]
    [MemberData(nameof(ParseManyData))]
    public void ParseMany(string input, IEnumerable<Tag> result)
    {
        var tags = Tag.Parse(input);
        tags.Should().BeEquivalentTo(result);
    }

    public static IEnumerable<object[]> ParseManyData()
    {
        yield return ["some memo #[vacation] #[gift=jimmy]", new Tag[] { new Tag("vacation"), new Tag("gift", "jimmy") }];
    }

    [Theory]
    [InlineData("vacation", null, "#[vacation]")]
    [InlineData("vacation", "disney", "#[vacation=disney]")]
    [InlineData("business trip", "kcdc conference", "#[business trip=kcdc conference]")]
    public void Encode(string name, string? value, string expected)
    {
        new Tag(name, value).Encode().Should().Be(expected);
    }

    [Theory]
    [InlineData("vacation", null)]
    [InlineData("vacation", "disney")]
    [InlineData("business trip", "kcdc conference")]
    public void EncodeRoundTrip(string name, string? value)
    {
        var tag = new Tag(name, value);
        Tag.Parse(tag.Encode()).Single().Should().Be(tag);
    }

    [Theory]
    [MemberData(nameof(ApplyTagsData))]
    public void ApplyTags(string? existingNotes, Tag[] tags, string expected)
    {
        Tag.ApplyTags(existingNotes, tags).Should().Be(expected);
    }

    public static IEnumerable<object?[]> ApplyTagsData() =>
    [
        ["grocery run", new[] { new Tag("food") }, "grocery run #[food]"],
        ["grocery run #[food]", new[] { new Tag("food"), new Tag("budget") }, "grocery run #[food] #[budget]"],
        ["grocery run #[old]", new[] { new Tag("food") }, "grocery run #[food]"],  // replaces existing tags
        [null, new[] { new Tag("food") }, "#[food]"],                              // null notes
        ["memo #[deleteme]", Array.Empty<Tag>(), "memo"],                                      // no tags clears encoded portion
        [null, Array.Empty<Tag>(), ""],                                            // null notes, no tags
    ];
}