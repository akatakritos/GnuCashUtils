using System.Collections.Generic;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using DynamicData;
using GnuCashUtils.Categorization;
using GnuCashUtils.Core;
using MediatR;
using NSubstitute;
using ReactiveUI;
using Unit = System.Reactive.Unit;

namespace GnuCashUtils.Tests.Categorization;

public class CategorizationWindowViewModelTests
{
    private readonly Fixture _fixture = new();

    class Fixture
    {
        public IMediator MockMediator = Substitute.For<IMediator>();
        public IConfigService MockConfigService = Substitute.For<IConfigService>();
        public IAccountStore MockAccountStore = Substitute.For<IAccountStore>();
        public IClassifierBuilder MockClassifierBuilder = Substitute.For<IClassifierBuilder>();

        private List<CsvRow> _transactions = SampleRows;

        public AppConfig AppConfig = new()
        {
            Banks = [SampleConfig],
            Database = "",
        };

        public Fixture WithTransactions(List<CsvRow> transactions)
        {
            _transactions = transactions;
            return this;
        }

        public Fixture WithBank(BankConfig bankConfig)
        {
            AppConfig.Banks.Add(bankConfig);
            return this;
        }

        public Fixture WithNoBanks()
        {
            AppConfig.Banks.Clear();
            return this;
        }

        public SourceCache<Account, string> Accounts = new(x => x.FullName);

        public CategorizationWindowViewModel BuildSubject()
        {
            MockMediator.Send(Arg.Any<ParseCsvRequest>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(_transactions));

            MockConfigService.CurrentConfig.Returns(AppConfig);

            Accounts.AddOrUpdate(SampleAccounts);
            MockAccountStore.Accounts.Returns(Accounts);
            MockAccountStore.AccountTree.Returns(Accounts);

            MockClassifierBuilder.Status.Returns(Observable.Never<ClassifierBuilder.BuilderStatus>());
            MockClassifierBuilder.Progress.Returns(Observable.Never<double>());
            MockClassifierBuilder.Build(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(BuildClassifier()));

            var vm = new CategorizationWindowViewModel(MockMediator, MockConfigService, MockAccountStore,
                MockClassifierBuilder);
            vm.ShowError.RegisterHandler(ctx => ctx.SetOutput(Unit.Default));
            vm.Activator.Activate();
            return vm;
        }


        public static readonly BankConfig SampleConfig = new()
        {
            Name = "Sample",
            Match = @"sample\.csv$",
            Skip = 1,
            Headers = "{date:yyyy-MM-dd},{description},{amount}",
            Account = "Expenses:Groceries"
        };

        private static readonly List<CsvRow> SampleRows =
        [
            new(new DateOnly(2024, 1, 15), "Grocery Store", -45.00m),
            new(new DateOnly(2024, 1, 16), "Gas Station", -60.00m),
            new(new DateOnly(2024, 1, 20), "Salary", 3000.00m),
        ];

        private static readonly List<Account> SampleAccounts =
        [
            new Account() { Guid = "groceries-guid", FullName = "Expenses:Groceries" }
        ];

        /// <summary>
        /// Returns a classifier trained so "Grocery Store" predicts "Expenses:Groceries"
        /// while "Gas Station" and "Salary" predict accounts not present in SampleAccounts.
        /// </summary>
        private static NaiveBayesianClassifier BuildClassifier()
        {
            var classifier = new NaiveBayesianClassifier(new Tokenizer());
            classifier.Train("Grocery Store", -45.00m, "Expenses:Groceries");
            classifier.Train("Gas Station", -60.00m, "Expenses:Fuel");
            classifier.Train("Salary", 3000.00m, "Income:Salary");
            return classifier;
        }
    }


    [Fact]
    public async Task ItPopulatesRowsFromHandlerResult()
    {
        var vm = _fixture.BuildSubject();
        await vm.LoadCsv(Fixtures.File("sample.csv"));

        vm.Rows.Should().HaveCount(3);
        vm.Rows[0].Date.Should().Be(new DateOnly(2024, 1, 15));
        vm.Rows[0].Description.Should().Be("Grocery Store");
        vm.Rows[0].Amount.Should().Be(-45.00m);
    }

    [Fact]
    public async Task ItSendsCorrectRequestToHandler()
    {
        var vm = _fixture.BuildSubject();
        var path = Fixtures.File("sample.csv");
        await vm.LoadCsv(path);

        await _fixture.MockMediator.Received(1).Send(
            Arg.Is<ParseCsvRequest>(r => r.FilePath == path && r.Config == _fixture.AppConfig.Banks[0]),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItSetsCsvFilePath()
    {
        var vm = _fixture.BuildSubject();
        var path = Fixtures.File("sample.csv");
        await vm.LoadCsv(path);

        vm.CsvFilePath.Should().Be(path);
    }

    [Fact]
    public async Task ItShowsErrorAndDoesNotLoadWhenNoBankConfigMatches()
    {
        var vm = _fixture.WithNoBanks().BuildSubject();
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
        var vm = _fixture.BuildSubject();
        await vm.LoadCsv(Fixtures.File("sample.csv"));

        // "Grocery Store" is predicted to a known account → valid; others predict unknown accounts → invalid
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
        var vm = _fixture.BuildSubject();
        await vm.LoadCsv(Fixtures.File("sample.csv"));
        vm.Rows.Should().HaveCount(3);

        vm.ShowOnlyErrors = true;
        vm.Rows.Should().HaveCount(2);

        vm.ShowOnlyErrors = false;
        vm.Rows.Should().HaveCount(3);
    }

    [Fact]
    public async Task ItAppliesClassifierPredictionWhenLoadingCsv()
    {
        var vm = _fixture.BuildSubject();
        await vm.LoadCsv(Fixtures.File("sample.csv"));

        var groceryRow = vm.Rows.Single(r => r.Description == "Grocery Store");
        groceryRow.SelectedAccount.Should().NotBeNull();
        groceryRow.SelectedAccount!.FullName.Should().Be("Expenses:Groceries");
        groceryRow.IsValid.Should().BeTrue();

        vm.Rows.Where(r => r.Description != "Grocery Store").Should().OnlyContain(r => !r.IsValid);

        vm.Rows.Count(x => !x.IsValid).Should().Be(2);
        vm.InvalidCount.Should().Be(2);
    }
}
