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

    private static (CategorizationWindowViewModel Vm, IMediator Mediator) Build(
        BankConfig? match = null,
        List<CsvRow>? rows = null)
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<FetchAccountsRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new List<Account>()));
        mediator.Send(Arg.Any<ParseCsvRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(rows ?? SampleRows));

        var configSvc = Substitute.For<IConfigService>();
        configSvc.CurrentConfig.Returns(new AppConfig { Banks = match != null ? [match] : [] });

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
    public async Task RowsShareTheAccountsCollection()
    {
        var (vm, _) = Build(SampleConfig);
        await vm.LoadCsv(Fixtures.File("sample.csv"));

        vm.Rows.Should().AllSatisfy(row => row.Accounts.Should().BeSameAs(vm.Accounts));
    }

    [Fact]
    public async Task MerchantDefaultsToEmpty()
    {
        var (vm, _) = Build(SampleConfig);
        await vm.LoadCsv(Fixtures.File("sample.csv"));

        vm.Rows.Should().AllSatisfy(row => row.Merchant.Should().BeEmpty());
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
}
