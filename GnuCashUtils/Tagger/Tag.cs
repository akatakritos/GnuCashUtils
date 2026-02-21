using System.Collections.Generic;
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

    [GeneratedRegex(@"#\[([^\]=]+?)(?:=([^\]]+?))?\]")]
    private static partial Regex FindTags();
}