using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using GnuCashUtils.BulkEdit;
using GnuCashUtils.Categorization;
using GnuCashUtils.Core;
using MediatR;
using NSubstitute;
using Unit = System.Reactive.Unit;

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

    private static readonly List<CsvRow> SampleRows =
    [
        new(new DateOnly(2024, 1, 15), "Grocery Store", -45.00m),
        new(new DateOnly(2024, 1, 16), "Gas Station", -60.00m),
        new(new DateOnly(2024, 1, 20), "Salary", 3000.00m),
    ];

    private static readonly List<MerchantConfig> SampleMerchants =
    [
        new() { Match = "contains(\"grocery\")", Name = "Grocery Store", Account = "Expenses:Groceries" },
    ];

    private static readonly List<Account> SampleAccounts =
    [
        new Account() { FullName = "Expenses:Groceries" }
    ];

    private static (CategorizationWindowViewModel Vm, IMediator Mediator) Build(
        BankConfig? match = null,
        List<CsvRow>? rows = null,
        List<MerchantConfig>? merchants = null
        )
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<FetchAccountsRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(SampleAccounts));
        mediator.Send(Arg.Any<ParseCsvRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(rows ?? SampleRows));

        var configSvc = Substitute.For<IConfigService>();
        configSvc.CurrentConfig.Returns(new AppConfig { Banks = match != null ? [match] : [], Merchants = merchants ?? SampleMerchants });

        return (new CategorizationWindowViewModel(mediator, configSvc), mediator);
    }

    [Fact]
    public async Task ItPopulatesRowsFromHandlerResult()
    {
        var (vm, _) = Build(SampleConfig);
        await vm.LoadCsv(Fixtures.File("sample.csv"));

        vm.Rows.Should().HaveCount(3);
        vm.Rows[0].Date.Should().Be(new DateOnly(2024, 1, 15));
        vm.Rows[0].Description.Should().Be("Grocery Store");
        vm.Rows[0].Amount.Should().Be(-45.00m);
    }

    [Fact]
    public async Task ItSendsCorrectRequestToHandler()
    {
        var (vm, mediator) = Build(SampleConfig);
        var path = Fixtures.File("sample.csv");
        await vm.LoadCsv(path);

        await mediator.Received(1).Send(
            Arg.Is<ParseCsvRequest>(r => r.FilePath == path && r.Config == SampleConfig),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItSetsCsvFilePath()
    {
        var (vm, _) = Build(SampleConfig);
        var path = Fixtures.File("sample.csv");
        await vm.LoadCsv(path);

        vm.CsvFilePath.Should().Be(path);
    }

    [Fact]
    public async Task ItShowsErrorAndDoesNotLoadWhenNoBankConfigMatches()
    {
        var (vm, _) = Build(match: null);
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

    [Fact]
    public async Task FilteringMaintainsOriginalOrder()
    {
        var (vm, _) = Build(match: SampleConfig, merchants: SampleMerchants);
        await vm.LoadCsv(Fixtures.File("sample.csv"));

        // Grocery Store (index 0) matched an account → valid; others are invalid
        vm.Rows[0].Description.Should().Be("Grocery Store");
        vm.Rows[1].Description.Should().Be("Gas Station");
        vm.Rows[2].Description.Should().Be("Salary");

        vm.ShowOnlyErrors = true;
        vm.Rows.Should().HaveCount(2);

        vm.ShowOnlyErrors = false;
        vm.Rows.Should().HaveCount(3);
        vm.Rows[0].Description.Should().Be("Grocery Store");
        vm.Rows[1].Description.Should().Be("Gas Station");
        vm.Rows[2].Description.Should().Be("Salary");
    }

    [Fact]
    public async Task Filtering()
    {
        var (vm,_) = Build(match: SampleConfig, merchants: SampleMerchants);
        await vm.LoadCsv(Fixtures.File("sample.csv"));
        vm.Rows.Should().HaveCount(3);

        vm.ShowOnlyErrors = true;
        vm.Rows.Should().HaveCount(2);

        vm.ShowOnlyErrors = false;
        vm.Rows.Should().HaveCount(3);
    }

    [Fact]
    public async Task ItAppliesMerchantMatchWhenLoadingCsv()
    {
        var (vm, _) = Build(match: SampleConfig, merchants: SampleMerchants);
        await vm.LoadCsv(Fixtures.File("sample.csv"));

        var groceryRow = vm.Rows.Single(r => r.Description == "Grocery Store");
        groceryRow.Merchant.Should().Be("Grocery Store");
        groceryRow.SelectedAccount.Should().NotBeNull();
        groceryRow.SelectedAccount!.FullName.Should().Be("Expenses:Groceries");
        groceryRow.IsValid.Should().BeTrue();

        vm.Rows.Where(r => r.Description != "Grocery Store").Should().OnlyContain(r => !r.IsValid);
        
        vm.Rows.Count(x => !x.IsValid).Should().Be(2);
        vm.InvalidCount.Should().Be(2);
    }
}
