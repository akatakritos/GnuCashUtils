using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace GnuCashUtils.Tagger;

public partial record Tag(string Name, string? Value = null)
{
    public override string ToString() => Encode();

    public string Encode() => string.IsNullOrEmpty(Value) ? $"#[{Name}]" : $"#[{Name}={Value}]";
    
    public static IEnumerable<Tag> Parse(string text)
    {
        foreach (Match match in FindTags().Matches(text))
        {
            var name = match.Groups[1].Value;
            var value = match.Groups[2].Success ? match.Groups[2].Value : null;
            yield return new Tag(name, value);
        }
    }

    private static string StripTags(string text) => FindTags().Replace(text, "").Trim();

    public static string ApplyTags(string? existingNotes, IEnumerable<Tag> tags)
    {
        var stripped = existingNotes == null ? "" : StripTags(existingNotes);
        var encoded = string.Join(" ", tags.Select(t => t.Encode()));
        if (string.IsNullOrEmpty(stripped)) return encoded;
        if (string.IsNullOrEmpty(encoded)) return stripped;
        return $"{stripped} {encoded}";
    }

    [GeneratedRegex(@"#\[([^\]=]+?)(?:=([^\]]+?))?\]")]
    private static partial Regex FindTags();
}