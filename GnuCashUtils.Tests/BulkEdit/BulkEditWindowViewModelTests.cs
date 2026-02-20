using System.Reactive.Concurrency;
using System.Reactive.Threading.Tasks;
using AwesomeAssertions;
using GnuCashUtils.BulkEdit;
using GnuCashUtils.Core;
using MediatR;
using Microsoft.Reactive.Testing;
using NSubstitute;
using ReactiveUI;
using ReactiveUI.Testing;

namespace GnuCashUtils.Tests.BulkEdit;

public class BulkEditWindowViewModelTests
{
    private readonly Fixture _fixture = new();

    class Fixture
    {
        public IMediator MockMediator = Substitute.For<IMediator>();
        public TestScheduler TestScheduler = new();

        private List<SelectableTransactionViewModel> _transactions = SampleTransactions;

        public static readonly Account SampleSourceAccount = new()
        {
            Guid = "source-guid",
            FullName = "Expenses:Groceries",
            Name = "Groceries"
        };

        public static readonly Account SampleDestinationAccount = new()
        {
            Guid = "destination-guid",
            FullName = "Expenses:Food",
            Name = "Food"
        };

        public static readonly List<SelectableTransactionViewModel> SampleTransactions =
        [
            new() { Description = "Grocery Store", Amount = -45.00m, Date = new DateTime(2024, 1, 15), SplitGuid = "split-1", TransactionGuid = "txn-1" },
            new() { Description = "Gas Station", Amount = -60.00m, Date = new DateTime(2024, 1, 16), SplitGuid = "split-2", TransactionGuid = "txn-2" },
            new() { Description = "Other Grocery", Amount = -30.00m, Date = new DateTime(2024, 1, 20), SplitGuid = "split-3", TransactionGuid = "txn-3" },
        ];

        public Fixture WithTransactions(List<SelectableTransactionViewModel> transactions)
        {
            _transactions = transactions;
            return this;
        }

        public BulkEditWindowViewModel BuildSubject()
        {
            // ImmediateScheduler makes ObserveOn(MainThreadScheduler) synchronous.
            // TestScheduler controls the Throttle so tests advance virtual time instead of waiting.
            RxApp.MainThreadScheduler = ImmediateScheduler.Instance;
            RxApp.TaskpoolScheduler = TestScheduler;

            MockMediator.Send(Arg.Any<FetchAccountsRequest>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(new List<Account> { SampleSourceAccount, SampleDestinationAccount }));

            MockMediator.Send(Arg.Any<FetchTransactions>(), Arg.Any<CancellationToken>())
                .Returns(_ => Task.FromResult(_transactions));

            MockMediator.Send(Arg.Any<MoveTransactionsCommand>(), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);

            return new BulkEditWindowViewModel(MockMediator, TestScheduler);
        }
    }

    // Fires the throttle by advancing virtual time, then waits a tick for the Task.Run inside
    // Observable.FromAsync to complete on the thread pool before the continuation resumes.
    private async Task SetSourceAccountAndWait(BulkEditWindowViewModel vm, Account account)
    {
        vm.SourceAccount = account;
        _fixture.TestScheduler.AdvanceByMs(300);
        await Task.Delay(10);
    }

    [Fact]
    public async Task ItPopulatesTransactionsWhenSourceAccountIsSet()
    {
        var vm = _fixture.BuildSubject();

        await SetSourceAccountAndWait(vm, Fixture.SampleSourceAccount);

        vm.Transactions.Should().HaveCount(3);
    }

    [Fact]
    public async Task ItSendsFetchTransactionsWithCorrectAccountGuid()
    {
        var vm = _fixture.BuildSubject();

        await SetSourceAccountAndWait(vm, Fixture.SampleSourceAccount);

        await _fixture.MockMediator.Received(1).Send(
            Arg.Is<FetchTransactions>(r => r.AccountGuid == Fixture.SampleSourceAccount.Guid),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SearchTextFiltersTransactions()
    {
        var vm = _fixture.BuildSubject();
        await SetSourceAccountAndWait(vm, Fixture.SampleSourceAccount);

        vm.SearchText = "Grocery";

        vm.Transactions.Should().HaveCount(2);
        vm.Transactions.Should().OnlyContain(t => t.Description!.Contains("Grocery"));
    }

    [Fact]
    public async Task SearchTextFilteringIsCaseInsensitive()
    {
        var vm = _fixture.BuildSubject();
        await SetSourceAccountAndWait(vm, Fixture.SampleSourceAccount);

        vm.SearchText = "grocery";

        vm.Transactions.Should().HaveCount(2);
    }

    [Fact]
    public async Task ClearingSearchTextRestoresAllTransactions()
    {
        var vm = _fixture.BuildSubject();
        await SetSourceAccountAndWait(vm, Fixture.SampleSourceAccount);

        vm.SearchText = "Grocery";
        vm.SearchText = "";

        vm.Transactions.Should().HaveCount(3);
    }

    [Fact]
    public async Task TransactionCountEmitsTotalFromAccount()
    {
        var vm = _fixture.BuildSubject();
        int? transactionCount = null;
        vm.TransactionCount.Subscribe(n => transactionCount = n);

        await SetSourceAccountAndWait(vm, Fixture.SampleSourceAccount);

        transactionCount.Should().Be(3);
    }

    [Fact]
    public async Task SelectAllCommandSelectsAllTransactions()
    {
        var vm = _fixture.BuildSubject();
        await SetSourceAccountAndWait(vm, Fixture.SampleSourceAccount);

        await vm.SelectAllCommand.Execute().ToTask();

        vm.Transactions.Should().OnlyContain(t => t.IsSelected);
    }

    [Fact]
    public void SelectAllCommandCannotExecuteWhenNoTransactionsLoaded()
    {
        var vm = _fixture.BuildSubject();
        bool canExecute = true;
        vm.SelectAllCommand.CanExecute.Subscribe(x => canExecute = x);

        canExecute.Should().BeFalse();
    }

    [Fact]
    public async Task SelectAllCommandCanExecuteAfterTransactionsLoaded()
    {
        var vm = _fixture.BuildSubject();
        bool canExecute = false;
        vm.SelectAllCommand.CanExecute.Subscribe(x => canExecute = x);

        await SetSourceAccountAndWait(vm, Fixture.SampleSourceAccount);

        canExecute.Should().BeTrue();
    }

    [Fact]
    public async Task MoveCommandSendsRequestWithCorrectSourceAndDestinationGuids()
    {
        var vm = _fixture.BuildSubject();
        await SetSourceAccountAndWait(vm, Fixture.SampleSourceAccount);
        vm.DestinationAccount = Fixture.SampleDestinationAccount;
        vm.Transactions[0].IsSelected = true;

        await vm.MoveCommand.Execute().ToTask();

        await _fixture.MockMediator.Received(1).Send(
            Arg.Is<MoveTransactionsCommand>(r =>
                r.SourceGuid == Fixture.SampleSourceAccount.Guid &&
                r.DestinationGuid == Fixture.SampleDestinationAccount.Guid),
            Arg.Any<CancellationToken>());
    }
}
