using AwesomeAssertions;
using GnuCashUtils.Categorization;
using GnuCashUtils.Core;

namespace GnuCashUtils.Tests.Categorization;

public class ParseCsvTests
{
    private static readonly BankConfig SampleConfig = new()
    {
        Name = "Sample",
        Match = @"sample\.csv$",
        Skip = 1,
        Headers = "{date:yyyy-MM-dd},{description},{amount}"
    };

    private static readonly BankConfig PreambleConfig = new()
    {
        Name = "Preamble",
        Match = @"preamble\.csv$",
        Skip = 5,
        Headers = "{date:yyyy-MM-dd},{description},{amount},_"
    };

    private static readonly BankConfig QuotedConfig = new()
    {
        Name = "Quoted",
        Match = @"quoted\.csv$",
        Skip = 1,
        Headers = "{date:yyyy-MM-dd},{description},{amount}"
    };

    private static readonly ParseCsvHandler Handler = new();

    private static Task<List<CsvRow>> Parse(string fixture, BankConfig config) =>
        Handler.Handle(new ParseCsvRequest(Fixtures.File(fixture), config), CancellationToken.None);

    [Fact]
    public async Task ItParsesDateDescriptionAndAmount()
    {
        var rows = await Parse("sample.csv", SampleConfig);

        rows.Should().HaveCount(3);
        rows[0].Date.Should().Be(new DateOnly(2024, 1, 15));
        rows[0].Description.Should().Be("Grocery Store");
        rows[0].Amount.Should().Be(-45.00m);
    }

    [Fact]
    public async Task ItSkipsPreambleRows()
    {
        // preamble.csv has 4 lines of bank header text then a CSV header row (5 total to skip)
        var rows = await Parse("preamble.csv", PreambleConfig);

        rows.Should().HaveCount(2);
        rows[0].Date.Should().Be(new DateOnly(2024, 1, 15));
        rows[0].Description.Should().Be("Grocery Store");
        rows[0].Amount.Should().Be(-45.00m);
    }

    [Fact]
    public async Task ItHandlesQuotedCommas()
    {
        var rows = await Parse("quoted.csv", QuotedConfig);

        rows.Should().HaveCount(3);
        rows[0].Description.Should().Be("Coffee, Snacks");
        rows[1].Description.Should().Be("Dinner, Drinks");
    }

    [Fact]
    public async Task ItHandlesEscapedQuotes()
    {
        var rows = await Parse("quoted.csv", QuotedConfig);

        // "Transfer ""savings""" should parse to: Transfer "savings"
        rows[2].Description.Should().Be("Transfer \"savings\"");
    }

    [Fact]
    public async Task ItSkipsTrailingBlankRows()
    {
        // sample.csv ends with a blank line; it should not appear as a row
        var rows = await Parse("sample.csv", SampleConfig);

        rows.Should().HaveCount(3);
    }

    [Fact]
    public async Task ItThrowsOnInvalidDate()
    {
        var badConfig = new BankConfig { Match = SampleConfig.Match, Skip = SampleConfig.Skip, Headers = "{date:MM/dd/yyyy},{description},{amount}" };

        var act = () => Parse("sample.csv", badConfig);

        await act.Should().ThrowAsync<FormatException>();
    }

    [Fact]
    public async Task ItThrowsOnInvalidAmount()
    {
        // Use description column as amount so parsing fails
        var badConfig = new BankConfig { Match = SampleConfig.Match, Skip = SampleConfig.Skip, Headers = "{date:yyyy-MM-dd},{amount},{description}" };

        var act = () => Parse("sample.csv", badConfig);

        await act.Should().ThrowAsync<FormatException>();
    }

    [Fact]
    public async Task ItNegatesAmountWhenUsingNegatedToken()
    {
        // negated.csv has BoA-style values: expenses are negative, deposits are positive
        // {-amount} should flip the sign so expenses become positive (debit to expense account)
        var config = new BankConfig
        {
            Name = "Negated",
            Match = @"negated\.csv$",
            Skip = 1,
            Headers = "{date:yyyy-MM-dd},{description},{-amount},_"
        };

        var rows = await Parse("negated.csv", config);

        rows.Should().HaveCount(3);
        rows[0].Amount.Should().Be(45.00m);   // -45.00 negated → expense debit
        rows[1].Amount.Should().Be(60.00m);   // -60.00 negated → expense debit
        rows[2].Amount.Should().Be(-3000.00m); // 3000.00 negated → deposit (credit)
    }
}
