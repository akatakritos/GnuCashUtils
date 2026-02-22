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

            MockMediator.Send(Arg.Any<FetchTags>())
                .Returns(Task.FromResult(new HashSet<Tag>()));

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
    public async Task ApplyCommand_AddOperation_AddsTagToTransaction()
    {
        var vm = _fixture.BuildSubject();
        await AdvancePastThrottle();

        vm.SelectedTransactions.Add(vm.Transactions[0]);
        vm.PendingOperations.Add(new TagOperation { Tag = Fixture.SampleTag, Operation = OperationType.Add });

        await vm.ApplyCommand.Execute().ToTask();

        vm.Transactions[0].Tags.Should().ContainSingle(t => t == Fixture.SampleTag);
    }

    [Fact]
    public async Task ApplyCommand_DeleteOperation_RemovesExistingTag()
    {
        var vm = _fixture.BuildSubject();
        await AdvancePastThrottle();

        var target = vm.Transactions[0];
        target.Tags.Add(Fixture.AnotherTag);

        vm.SelectedTransactions.Add(target);
        vm.PendingOperations.Add(new TagOperation { Tag = Fixture.AnotherTag, Operation = OperationType.Delete });
        await vm.ApplyCommand.Execute().ToTask();

        target.Tags.Should().BeEmpty();
    }

    [Fact]
    public async Task ApplyCommand_AddOperation_MarksTransactionDirty()
    {
        var vm = _fixture.BuildSubject();
        await AdvancePastThrottle();

        var target = vm.Transactions[0];
        target.IsDirty = false;

        vm.SelectedTransactions.Add(target);
        vm.PendingOperations.Add(new TagOperation { Tag = Fixture.SampleTag, Operation = OperationType.Add });
        await vm.ApplyCommand.Execute().ToTask();

        target.IsDirty.Should().BeTrue();
    }

    [Fact]
    public async Task ApplyCommand_AppliesToAllSelectedTransactions()
    {
        var vm = _fixture.BuildSubject();
        await AdvancePastThrottle();

        vm.SelectedTransactions.Add(vm.Transactions[0]);
        vm.SelectedTransactions.Add(vm.Transactions[1]);
        vm.PendingOperations.Add(new TagOperation { Tag = Fixture.SampleTag, Operation = OperationType.Add });

        await vm.ApplyCommand.Execute().ToTask();

        vm.Transactions[0].Tags.Should().Contain(Fixture.SampleTag);
        vm.Transactions[1].Tags.Should().Contain(Fixture.SampleTag);
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
    public async Task SaveCommandClearsDirtyFlagAfterSave()
    {
        var vm = _fixture.BuildSubject();
        await AdvancePastThrottle();

        vm.Transactions[0].IsDirty = true;
        vm.Transactions[1].IsDirty = true;
        vm.Transactions[2].IsDirty = false;

        await vm.SaveCommand.Execute().ToTask();

        vm.Transactions[0].IsDirty.Should().BeFalse();
        vm.Transactions[1].IsDirty.Should().BeFalse();
        vm.Transactions[2].IsDirty.Should().BeFalse();
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
    public async Task AddTagCommandAddsTagToPendingOperationsAsAdd()
    {
        var vm = _fixture.BuildSubject();

        await vm.AddTagCommand.Execute(Fixture.SampleTag).ToTask();

        vm.PendingOperations.Should().ContainSingle(t => t.Tag == Fixture.SampleTag && t.Operation == OperationType.Add);
    }

    [Fact]
    public async Task AddTagCommandIgnoresDuplicates()
    {
        var vm = _fixture.BuildSubject();

        await vm.AddTagCommand.Execute(Fixture.SampleTag).ToTask();
        await vm.AddTagCommand.Execute(Fixture.SampleTag).ToTask();

        vm.PendingOperations.Should().ContainSingle(t => t.Tag == Fixture.SampleTag);
    }

    [Fact]
    public async Task AddTagCommand_WhenExistingOpIsNone_SetsToAdd()
    {
        var vm = _fixture.BuildSubject();
        vm.PendingOperations.Add(new TagOperation { Tag = Fixture.SampleTag, Operation = OperationType.None });

        await vm.AddTagCommand.Execute(Fixture.SampleTag).ToTask();

        vm.PendingOperations.Should().ContainSingle(t => t.Tag == Fixture.SampleTag && t.Operation == OperationType.Add);
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

    [Fact]
    public async Task WhenNewSearchLoads_DirtyTransactionsArePreserved()
    {
        var vm = await BuildAndDrainInitial();

        // Mark the first transaction dirty
        vm.Transactions[0].IsDirty = true;

        // New search returns only txn-2 and txn-3 (txn-1 is gone from results)
        _fixture.Transactions = [Fixture.MakeSampleTransactions()[1], Fixture.MakeSampleTransactions()[2]];
        vm.SearchText = "new search";
        await AdvancePastThrottle();

        vm.Transactions.Should().HaveCount(3);
        vm.Transactions.Should().Contain(t => t.TransactionGuid == "txn-1" && t.IsDirty);
    }

    [Fact]
    public async Task WhenNewSearchLoads_DirtyTransactionIsNotOverwrittenBySearchResult()
    {
        var vm = await BuildAndDrainInitial();

        // Add a tag to txn-1 — this marks it dirty and records a change
        var dirtyTxn = vm.Transactions[0];
        dirtyTxn.Tags.Add(Fixture.SampleTag);

        // New search returns fresh objects with the same GUIDs (including txn-1 without the tag)
        _fixture.Transactions = Fixture.MakeSampleTransactions();
        vm.SearchText = "new search";
        await AdvancePastThrottle();

        // The dirty in-memory instance must be kept — not replaced by the fresh one
        vm.Transactions.Single(t => t.TransactionGuid == "txn-1").Should().BeSameAs(dirtyTxn);
        vm.Transactions.Single(t => t.TransactionGuid == "txn-1").Tags.Should().ContainSingle(t => t == Fixture.SampleTag);
    }

    [Fact]
    public async Task WhenNewSearchLoads_CleanTransactionsAreReplaced()
    {
        var vm = await BuildAndDrainInitial();

        var originalTxn1 = vm.Transactions[0];
        // txn-1 stays clean

        // New search returns fresh objects with the same GUIDs
        _fixture.Transactions = Fixture.MakeSampleTransactions();
        vm.SearchText = "new search";
        await AdvancePastThrottle();

        // Clean transaction should be replaced by the new instance from search
        vm.Transactions.Single(t => t.TransactionGuid == "txn-1").Should().NotBeSameAs(originalTxn1);
    }

    [Fact]
    public async Task SelectingTransaction_RebuildsPendingOperationsFromTransactionTags()
    {
        var vm = _fixture.BuildSubject();
        await AdvancePastThrottle();

        var target = vm.Transactions[0];
        target.Tags.Add(Fixture.SampleTag);
        target.IsDirty = false;

        vm.SelectedTransactions.Add(target);

        vm.PendingOperations.Should().ContainSingle(op => op.Tag == Fixture.SampleTag && op.Operation == OperationType.None);
    }

    [Fact]
    public async Task SelectingMultipleTransactions_MergesTagsAsUnion()
    {
        var vm = _fixture.BuildSubject();
        await AdvancePastThrottle();

        var txn1 = vm.Transactions[0];
        txn1.Tags.Add(Fixture.SampleTag);
        txn1.IsDirty = false;

        var txn2 = vm.Transactions[1];
        txn2.Tags.Add(Fixture.AnotherTag);
        txn2.IsDirty = false;

        vm.SelectedTransactions.Add(txn1);
        vm.SelectedTransactions.Add(txn2);

        vm.PendingOperations.Should().HaveCount(2);
        vm.PendingOperations.Should().Contain(op => op.Tag == Fixture.SampleTag && op.Operation == OperationType.None);
        vm.PendingOperations.Should().Contain(op => op.Tag == Fixture.AnotherTag && op.Operation == OperationType.None);
    }

    [Fact]
    public async Task SelectingMultipleTransactions_DeduplicatesSharedTags()
    {
        var vm = _fixture.BuildSubject();
        await AdvancePastThrottle();

        var txn1 = vm.Transactions[0];
        txn1.Tags.Add(Fixture.SampleTag);
        txn1.IsDirty = false;

        var txn2 = vm.Transactions[1];
        txn2.Tags.Add(Fixture.SampleTag);
        txn2.IsDirty = false;

        vm.SelectedTransactions.Add(txn1);
        vm.SelectedTransactions.Add(txn2);

        vm.PendingOperations.Should().ContainSingle(op => op.Tag == Fixture.SampleTag);
    }

    [Fact]
    public async Task DeselectingAll_ClearsPendingOperations()
    {
        var vm = _fixture.BuildSubject();
        await AdvancePastThrottle();

        var target = vm.Transactions[0];
        target.Tags.Add(Fixture.SampleTag);
        target.IsDirty = false;

        vm.SelectedTransactions.Add(target);
        vm.SelectedTransactions.Clear();

        vm.PendingOperations.Should().BeEmpty();
    }

    [Fact]
    public async Task CycleOperationCommand_CyclesNoneAddDeleteNone()
    {
        var vm = _fixture.BuildSubject();
        var op = new TagOperation { Tag = Fixture.SampleTag, Operation = OperationType.None };

        await vm.CycleOperationCommand.Execute(op).ToTask();
        op.Operation.Should().Be(OperationType.Add);

        await vm.CycleOperationCommand.Execute(op).ToTask();
        op.Operation.Should().Be(OperationType.Delete);

        await vm.CycleOperationCommand.Execute(op).ToTask();
        op.Operation.Should().Be(OperationType.None);
    }
}
