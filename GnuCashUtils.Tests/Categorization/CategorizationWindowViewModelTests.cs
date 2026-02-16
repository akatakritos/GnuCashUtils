using System;
using System.Reactive;
using System.Threading.Tasks;
using AwesomeAssertions;
using GnuCashUtils.Categorization;
using GnuCashUtils.Core;
using NSubstitute;

namespace GnuCashUtils.Tests.Categorization;

public class CategorizationWindowViewModelTests
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

    private static IConfigService Config(params BankConfig[] banks)
    {
        var svc = Substitute.For<IConfigService>();
        svc.CurrentConfig.Returns(new AppConfig { Banks = [.. banks] });
        return svc;
    }

    [Fact]
    public async Task ItLoadsCsv()
    {
        var vm = new CategorizationWindowViewModel(configService: Config(SampleConfig));
        await vm.LoadCsv(Fixtures.File("sample.csv"));

        vm.Rows.Should().HaveCount(3);
        vm.Rows[0].Date.Should().Be(new DateOnly(2024, 1, 15));
        vm.Rows[0].Description.Should().Be("Grocery Store");
        vm.Rows[0].Amount.Should().Be(-45.00m);
    }

    [Fact]
    public async Task ItSkipsPreamble()
    {
        // preamble.csv has 4 lines of bank header text then a CSV header row (5 total to skip)
        var vm = new CategorizationWindowViewModel(configService: Config(PreambleConfig));
        await vm.LoadCsv(Fixtures.File("preamble.csv"));

        vm.Rows.Should().HaveCount(2);
        vm.Rows[0].Date.Should().Be(new DateOnly(2024, 1, 15));
        vm.Rows[0].Description.Should().Be("Grocery Store");
        vm.Rows[0].Amount.Should().Be(-45.00m);
    }

    [Fact]
    public async Task ItHandlesQuotedCommas()
    {
        var vm = new CategorizationWindowViewModel(configService: Config(QuotedConfig));
        await vm.LoadCsv(Fixtures.File("quoted.csv"));

        vm.Rows.Should().HaveCount(3);
        vm.Rows[0].Description.Should().Be("Coffee, Snacks");
        vm.Rows[1].Description.Should().Be("Dinner, Drinks");
    }

    [Fact]
    public async Task ItHandlesEscapedQuotes()
    {
        var vm = new CategorizationWindowViewModel(configService: Config(QuotedConfig));
        await vm.LoadCsv(Fixtures.File("quoted.csv"));

        // "Transfer ""savings""" should parse to: Transfer "savings"
        vm.Rows[2].Description.Should().Be("Transfer \"savings\"");
    }

    [Fact]
    public async Task ItSkipsTrailingBlankRows()
    {
        // sample.csv ends with a blank line; it should not appear as a row
        var vm = new CategorizationWindowViewModel(configService: Config(SampleConfig));
        await vm.LoadCsv(Fixtures.File("sample.csv"));

        vm.Rows.Should().HaveCount(3);
    }

    [Fact]
    public async Task ItSetsCsvFilePath()
    {
        var vm = new CategorizationWindowViewModel(configService: Config(SampleConfig));
        var path = Fixtures.File("sample.csv");
        await vm.LoadCsv(path);

        vm.CsvFilePath.Should().Be(path);
    }

    [Fact]
    public async Task RowsShareTheAccountsCollection()
    {
        var vm = new CategorizationWindowViewModel(configService: Config(SampleConfig));
        await vm.LoadCsv(Fixtures.File("sample.csv"));

        vm.Rows.Should().AllSatisfy(row => row.Accounts.Should().BeSameAs(vm.Accounts));
    }

    [Fact]
    public async Task MerchantDefaultsToEmpty()
    {
        var vm = new CategorizationWindowViewModel(configService: Config(SampleConfig));
        await vm.LoadCsv(Fixtures.File("sample.csv"));

        vm.Rows.Should().AllSatisfy(row => row.Merchant.Should().BeEmpty());
    }

    [Fact]
    public async Task ItShowsErrorAndDoesNotLoadWhenNoBankConfigMatches()
    {
        var vm = new CategorizationWindowViewModel(configService: Config());
        string? errorMessage = null;
        vm.ShowError.RegisterHandler(ctx =>
        {
            errorMessage = ctx.Input;
            ctx.SetOutput(Unit.Default);
        });

        await vm.LoadCsv(Fixtures.File("sample.csv"));

        vm.Rows.Should().BeEmpty();
        errorMessage.Should().Contain("sample.csv");
    }
}
