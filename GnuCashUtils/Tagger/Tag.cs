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

    /// <summary>
    /// Parses a user-typed string into a Tag. Accepts "#[name]", "#[name=value]", "name", or "name=value".
    /// Returns null if the input is blank or unparseable.
    /// </summary>
    public static Tag? FromInput(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        input = input.Trim();

        // Already in full #[...] format?
        var tags = Parse(input).ToList();
        if (tags.Count == 1) return tags[0];

        // Simple "name" or "name=value"
        var eqIndex = input.IndexOf('=');
        if (eqIndex < 0) return new Tag(input);

        var name = input[..eqIndex].Trim();
        var value = input[(eqIndex + 1)..].Trim();
        return string.IsNullOrEmpty(name) ? null : new Tag(name, string.IsNullOrEmpty(value) ? null : value);
    }

    [GeneratedRegex(@"#\[([^\]=]+?)(?:=([^\]]+?))?\]")]
    private static partial Regex FindTags();
}