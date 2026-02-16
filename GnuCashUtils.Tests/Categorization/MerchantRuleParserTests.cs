using AwesomeAssertions;
using GnuCashUtils.Categorization;
using GnuCashUtils.Core;

namespace GnuCashUtils.Tests.Categorization;

public class MerchantRuleParserTests
{
    private static CategorizationRowViewModel Row(string desc, decimal amount = 0m) =>
        new(DateOnly.MinValue, desc, amount);

    private static Func<CategorizationRowViewModel, bool> Compile(string rule) =>
        new MerchantRuleParser().Parse(rule).Compile();

    // ── contains ────────────────────────────────────────────────────────────

    [Fact]
    public void Contains_MatchesWhenDescriptionContainsText()
    {
        var fn = Compile(@"contains(""AMAZON WEB SERVICES"")");
        fn(Row("AMAZON WEB SERVICES #12345")).Should().BeTrue();
    }

    [Fact]
    public void Contains_IsCaseInsensitive()
    {
        var fn = Compile(@"contains(""amazon"")");
        fn(Row("AMAZON PRIME")).Should().BeTrue();
    }

    [Fact]
    public void Contains_DoesNotMatchWhenTextAbsent()
    {
        var fn = Compile(@"contains(""AMAZON"")");
        fn(Row("Walmart #123")).Should().BeFalse();
    }

    // ── startswith ───────────────────────────────────────────────────────────

    [Fact]
    public void StartsWith_MatchesWhenDescriptionStartsWithText()
    {
        var fn = Compile(@"startswith(""AMAZON"")");
        fn(Row("AMAZON PRIME")).Should().BeTrue();
    }

    [Fact]
    public void StartsWith_IsCaseInsensitive()
    {
        var fn = Compile(@"startswith(""amazon"")");
        fn(Row("AMAZON PRIME")).Should().BeTrue();
    }

    [Fact]
    public void StartsWith_DoesNotMatchMidString()
    {
        var fn = Compile(@"startswith(""PRIME"")");
        fn(Row("AMAZON PRIME")).Should().BeFalse();
    }

    // ── endswith ─────────────────────────────────────────────────────────────

    [Fact]
    public void EndsWith_MatchesWhenDescriptionEndsWithText()
    {
        var fn = Compile(@"endswith(""PRIME"")");
        fn(Row("AMAZON PRIME")).Should().BeTrue();
    }

    [Fact]
    public void EndsWith_IsCaseInsensitive()
    {
        var fn = Compile(@"endswith(""prime"")");
        fn(Row("AMAZON PRIME")).Should().BeTrue();
    }

    [Fact]
    public void EndsWith_DoesNotMatchPrefix()
    {
        var fn = Compile(@"endswith(""AMAZON"")");
        fn(Row("AMAZON PRIME")).Should().BeFalse();
    }

    // ── oneof ────────────────────────────────────────────────────────────────

    [Fact]
    public void OneOf_MatchesFirstTerm()
    {
        var fn = Compile(@"oneof(""coffee"", ""starbucks"")");
        fn(Row("Blue Bottle Coffee")).Should().BeTrue();
    }

    [Fact]
    public void OneOf_MatchesSecondTerm()
    {
        var fn = Compile(@"oneof(""coffee"", ""starbucks"")");
        fn(Row("STARBUCKS #123")).Should().BeTrue();
    }

    [Fact]
    public void OneOf_IsCaseInsensitive()
    {
        var fn = Compile(@"oneof(""COFFEE"")");
        fn(Row("Blue Bottle coffee")).Should().BeTrue();
    }

    [Fact]
    public void OneOf_DoesNotMatchWhenNonePresent()
    {
        var fn = Compile(@"oneof(""coffee"", ""starbucks"")");
        fn(Row("McDonald's")).Should().BeFalse();
    }

    // ── regex ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Regex_MatchesWhenPatternMatches()
    {
        var fn = Compile(@"regex(""QT\d+"")");
        fn(Row("QT1234 GAS STATION")).Should().BeTrue();
    }

    [Fact]
    public void Regex_IsCaseInsensitive()
    {
        var fn = Compile(@"regex(""amazon"")");
        fn(Row("AMAZON PRIME")).Should().BeTrue();
    }

    [Fact]
    public void Regex_DoesNotMatchWhenPatternFails()
    {
        var fn = Compile(@"regex(""^\d+$"")");
        fn(Row("AMAZON PRIME")).Should().BeFalse();
    }

    [Fact]
    public void Regex_MatchesAnything()
    {
        var fn = Compile(@"regex(""."")");
        fn(Row("anything")).Should().BeTrue();
    }

    // ── amount comparisons ───────────────────────────────────────────────────

    [Fact]
    public void AmountGt_MatchesWhenGreater()
    {
        var fn = Compile("amount > 10");
        fn(Row("", 10.01m)).Should().BeTrue();
        fn(Row("", 10.00m)).Should().BeFalse();
        fn(Row("", 9.99m)).Should().BeFalse();
    }

    [Fact]
    public void AmountLt_MatchesWhenLess()
    {
        var fn = Compile("amount < 10");
        fn(Row("", 9.99m)).Should().BeTrue();
        fn(Row("", 10.00m)).Should().BeFalse();
    }

    [Fact]
    public void AmountGte_MatchesWhenGreaterOrEqual()
    {
        var fn = Compile("amount >= 10");
        fn(Row("", 10.00m)).Should().BeTrue();
        fn(Row("", 10.01m)).Should().BeTrue();
        fn(Row("", 9.99m)).Should().BeFalse();
    }

    [Fact]
    public void AmountLte_MatchesWhenLessOrEqual()
    {
        var fn = Compile("amount <= 10");
        fn(Row("", 10.00m)).Should().BeTrue();
        fn(Row("", 9.99m)).Should().BeTrue();
        fn(Row("", 10.01m)).Should().BeFalse();
    }

    [Fact]
    public void AmountEq_MatchesWhenEqual()
    {
        var fn = Compile("amount == 42.50");
        fn(Row("", 42.50m)).Should().BeTrue();
        fn(Row("", 42.00m)).Should().BeFalse();
    }

    [Fact]
    public void AmountNeq_MatchesWhenNotEqual()
    {
        var fn = Compile("amount != 0");
        fn(Row("", 1.00m)).Should().BeTrue();
        fn(Row("", 0.00m)).Should().BeFalse();
    }

    // ── and / or ─────────────────────────────────────────────────────────────

    [Fact]
    public void And_RequiresBothConditionsTrue()
    {
        var fn = Compile(@"contains(""QT"") and amount > 10");
        fn(Row("QT GAS", 15m)).Should().BeTrue();
        fn(Row("QT GAS", 5m)).Should().BeFalse();
        fn(Row("Walmart", 15m)).Should().BeFalse();
    }

    [Fact]
    public void Or_TrueWhenEitherConditionTrue()
    {
        var fn = Compile(@"contains(""QT"") or contains(""Shell"")");
        fn(Row("QT GAS")).Should().BeTrue();
        fn(Row("Shell Station")).Should().BeTrue();
        fn(Row("BP Gas")).Should().BeFalse();
    }

    [Fact]
    public void Or_HasLowerPrecedenceThanAnd()
    {
        // a or b and c  =>  a or (b and c)
        var fn = Compile(@"contains(""A"") or contains(""B"") and amount > 10");
        fn(Row("A", 0m)).Should().BeTrue();   // A matches alone via OR
        fn(Row("B", 15m)).Should().BeTrue();  // B and amount > 10
        fn(Row("B", 5m)).Should().BeFalse();  // B but amount not > 10
    }

    [Fact]
    public void Grouping_OverridesPrecedence()
    {
        // (a or b) and amount > 10
        var fn = Compile(@"(contains(""A"") or contains(""B"")) and amount > 10");
        fn(Row("A", 15m)).Should().BeTrue();
        fn(Row("A", 5m)).Should().BeFalse();  // amount fails
        fn(Row("C", 15m)).Should().BeFalse(); // neither A nor B
    }

    // ── invalid syntax ───────────────────────────────────────────────────────

    [Fact]
    public void InvalidSyntax_UnknownKeyword_Throws()
    {
        var act = () => Compile("matches(\"foo\")");
        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void InvalidSyntax_MissingClosingParen_Throws()
    {
        var act = () => Compile(@"contains(""foo""");
        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void InvalidSyntax_MissingOperatorAfterAmount_Throws()
    {
        var act = () => Compile("amount 10");
        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void InvalidSyntax_UnterminatedString_Throws()
    {
        var act = () => Compile(@"contains(""foo)");
        act.Should().Throw<FormatException>();
    }

    // ── MerchantMatcher ──────────────────────────────────────────────────────

    [Fact]
    public void MerchantMatcher_ReturnsFirstMatchingName()
    {
        var matcher = new MerchantMatcher([
            new MerchantConfig { Name = "AWS", Match = @"contains(""AMAZON WEB SERVICES"")" },
            new MerchantConfig { Name = "Amazon", Match = @"startswith(""AMAZON"")" },
        ]);

        matcher.Match(Row("AMAZON WEB SERVICES #1"))!.Name.Should().Be("AWS");
        matcher.Match(Row("AMAZON PRIME"))!.Name.Should().Be("Amazon");
    }

    [Fact]
    public void MerchantMatcher_ReturnsNullWhenNoRuleMatches()
    {
        var matcher = new MerchantMatcher([
            new MerchantConfig { Name = "AWS", Match = @"contains(""AMAZON WEB SERVICES"")" },
        ]);

        matcher.Match(Row("Walmart")).Should().BeNull();
    }

    [Fact]
    public void MerchantMatcher_SkipsConfigsWithNoRule()
    {
        var matcher = new MerchantMatcher([
            new MerchantConfig { Name = "Empty" },
            new MerchantConfig { Name = "AWS", Match = @"contains(""AMAZON"")" },
        ]);

        matcher.Match(Row("AMAZON"))!.Name.Should().Be("AWS");
    }

    [Fact]
    public void MerchantMatcher_ReturnsNullForEmptyRuleList()
    {
        var matcher = new MerchantMatcher([]);
        matcher.Match(Row("AMAZON")).Should().BeNull();
    }
}
