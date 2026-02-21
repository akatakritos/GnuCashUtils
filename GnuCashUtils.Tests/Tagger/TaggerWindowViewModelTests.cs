using System.Reactive.Concurrency;
using System.Reactive.Threading.Tasks;
using AwesomeAssertions;
using GnuCashUtils.Core;
using GnuCashUtils.Tagger;
using MediatR;
using Microsoft.Reactive.Testing;
using NSubstitute;
using ReactiveUI;
using ReactiveUI.Testing;

namespace GnuCashUtils.Tests.Tagger;

public class TaggerWindowViewModelTests
{
    private readonly Fixture _fixture = new();

    class Fixture
    {
        public IMediator MockMediator = Substitute.For<IMediator>();
        public TestScheduler TestScheduler = new();

        // Mutable so tests can change the return value via the captured closure.
        public List<TaggedTransaction> Transactions = MakeSampleTransactions();

        public static readonly Account SampleAccount = new() { Guid = "acc-1", Name = "Checking", FullName = "Assets:Checking" };

        public static List<TaggedTransaction> MakeSampleTransactions() =>
        [
            new() { TransactionGuid = "txn-1", Description = "Coffee Shop", Amount = -5.00m, Date = new DateOnly(2024, 1, 10), Account = SampleAccount },
            new() { TransactionGuid = "txn-2", Description = "Gas Station", Amount = -60.00m, Date = new DateOnly(2024, 1, 15), Account = SampleAccount },
            new() { TransactionGuid = "txn-3", Description = "Grocery Store", Amount = -120.00m, Date = new DateOnly(2024, 1, 20), Account = SampleAccount },
        ];

        public static readonly Tag SampleTag = new("food", null);
        public static readonly Tag AnotherTag = new("transport", null);

        public TaggerWindowViewModel BuildSubject()
        {
            RxApp.MainThreadScheduler = ImmediateScheduler.Instance;

            MockMediator.Send(Arg.Any<SearchTransactions>(), Arg.Any<CancellationToken>())
                .Returns(_ => Task.FromResult(Transactions));

            MockMediator.Send(Arg.Any<ApplyTags>(), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);

            return new TaggerWindowViewModel(MockMediator, TestScheduler);
        }
    }

    // Advances past the 250ms virtual throttle and waits a tick for the async task to complete.
    private async Task AdvancePastThrottle()
    {
        _fixture.TestScheduler.AdvanceByMs(300);
        await Task.CompletedTask;
    }

    // Builds the VM, drains the automatic initial WhenAnyValue emission, and clears recorded calls.
    private async Task<TaggerWindowViewModel> BuildAndDrainInitial()
    {
        var vm = _fixture.BuildSubject();
        await AdvancePastThrottle();
        _fixture.MockMediator.ClearReceivedCalls();
        return vm;
    }

    [Fact]
    public async Task ItPopulatesTransactionsWhenSearchParamsChange()
    {
        var vm = _fixture.BuildSubject();

        vm.SearchText = "Coffee";
        await AdvancePastThrottle();

        vm.Transactions.Should().HaveCount(3);
    }

    [Fact]
    public async Task ItSendsSearchTransactionsWithCorrectSearchText()
    {
        var vm = await BuildAndDrainInitial();

        vm.SearchText = "Coffee";
        await AdvancePastThrottle();

        await _fixture.MockMediator.Received(1).Send(
            Arg.Is<SearchTransactions>(r => r.SearchText == "Coffee"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItSendsSearchTransactionsWithDateRange()
    {
        var vm = await BuildAndDrainInitial();
        var start = new DateOnly(2024, 1, 1);
        var end = new DateOnly(2024, 1, 31);

        vm.StartDate = start;
        vm.EndDate = end;
        await AdvancePastThrottle();

        await _fixture.MockMediator.Received(1).Send(
            Arg.Is<SearchTransactions>(r => r.StartDate == start && r.EndDate == end),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItClearsOldTransactionsBeforePopulatingNew()
    {
        var vm = await BuildAndDrainInitial();

        _fixture.Transactions = [Fixture.MakeSampleTransactions()[0]];
        vm.SearchText = "second";
        await AdvancePastThrottle();

        vm.Transactions.Should().HaveCount(1);
    }

    [Fact]
    public async Task ApplyCommandSetsSelectedTagsOnSelectedTransaction()
    {
        var vm = _fixture.BuildSubject();
        await AdvancePastThrottle();

        vm.SelectedTransaction = vm.Transactions[0];
        vm.SelectedTags.Add(Fixture.SampleTag);

        await vm.ApplyCommand.Execute().ToTask();

        vm.Transactions[0].Tags.Should().ContainSingle(t => t == Fixture.SampleTag);
    }

    [Fact]
    public async Task ApplyCommandReplacesExistingTags()
    {
        var vm = _fixture.BuildSubject();
        await AdvancePastThrottle();

        var target = vm.Transactions[0];
        target.Tags.Add(Fixture.AnotherTag);

        vm.SelectedTransaction = target;
        vm.SelectedTags.Add(Fixture.SampleTag);
        await vm.ApplyCommand.Execute().ToTask();

        target.Tags.Should().ContainSingle(t => t == Fixture.SampleTag);
    }

    [Fact]
    public async Task ApplyCommandMarksTransactionDirty()
    {
        var vm = _fixture.BuildSubject();
        await AdvancePastThrottle();

        var target = vm.Transactions[0];
        target.IsDirty = false;

        vm.SelectedTransaction = target;
        vm.SelectedTags.Add(Fixture.SampleTag);
        await vm.ApplyCommand.Execute().ToTask();

        target.IsDirty.Should().BeTrue();
    }

    [Fact]
    public async Task SaveCommandSendsOnlyDirtyTransactions()
    {
        var vm = _fixture.BuildSubject();
        await AdvancePastThrottle();

        vm.Transactions[0].IsDirty = true;
        vm.Transactions[1].IsDirty = false;
        vm.Transactions[2].IsDirty = true;

        await vm.SaveCommand.Execute().ToTask();

        await _fixture.MockMediator.Received(1).Send(
            Arg.Is<ApplyTags>(r => r.Transactions.Count() == 2),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveCommandDoesNotSendCleanTransactions()
    {
        var vm = _fixture.BuildSubject();
        await AdvancePastThrottle();

        foreach (var t in vm.Transactions) t.IsDirty = false;

        await vm.SaveCommand.Execute().ToTask();

        await _fixture.MockMediator.Received(1).Send(
            Arg.Is<ApplyTags>(r => r.Transactions.Count() == 0),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void TaggedTransactionIsNotDirtyInitially()
    {
        var transaction = new TaggedTransaction { Account = Fixture.SampleAccount };

        transaction.IsDirty.Should().BeFalse();
    }

    [Fact]
    public void TaggedTransactionBecomesDirtyWhenTagAdded()
    {
        var transaction = new TaggedTransaction { Account = Fixture.SampleAccount };

        transaction.Tags.Add(Fixture.SampleTag);

        transaction.IsDirty.Should().BeTrue();
    }

    [Fact]
    public void TaggedTransactionBecomesDirtyWhenTagRemoved()
    {
        var transaction = new TaggedTransaction { Account = Fixture.SampleAccount };
        transaction.Tags.Add(Fixture.SampleTag);
        transaction.IsDirty = false;

        transaction.Tags.Remove(Fixture.SampleTag);

        transaction.IsDirty.Should().BeTrue();
    }
}
