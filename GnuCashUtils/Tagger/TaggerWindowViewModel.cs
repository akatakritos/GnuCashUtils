using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using DynamicData;
using DynamicData.Binding;
using GnuCashUtils.Core;
using MediatR;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Serilog;
using Unit = System.Reactive.Unit;

namespace GnuCashUtils.Tagger;

// TODO: search should include tag
// TODO: make it prettier

public partial class TaggerWindowViewModel : ViewModelBase, IActivatableViewModel
{
    private static readonly ILogger _log = Log.ForContext<TaggerWindowViewModel>();
    private readonly IMediator _mediator;

    private SourceCache<Tag, Tag> _tagsCache = new(x => x);
    [ObservableAsProperty] public partial IReadOnlyCollection<Tag> Tags { get; }

    [Reactive] public partial string SearchText { get; set; }
    [Reactive] public partial DateOnly? StartDate { get; set; }
    [Reactive] public partial DateOnly? EndDate { get; set; }

    public ObservableCollection<TaggedTransaction> SelectedTransactions { get; } = [];

    /// <summary>
    /// A list of operations that are pending to be applied to the selected transactions.
    /// </summary>
    public ObservableCollection<TagOperation> PendingOperations { get; } = [];

    /// <summary>
    /// Adds a tag selected from the autocomplete into the pending operations as Add.
    /// If an operation already exists for that tag, its operation is set to Add.
    /// </summary>
    public ReactiveCommand<Tag, Unit> AddTagCommand { get; }

    /// <summary>
    /// Adds a brand new tag from the autocomplete into the list of tags.
    /// </summary>
    public ReactiveCommand<Tag, Unit> AddNewTagCommand { get; }

    /// <summary>
    /// Cycles the operation of a TagOperation: None → Add → Delete → None.
    /// </summary>
    public ReactiveCommand<TagOperation, Unit> CycleOperationCommand { get; }

    /// <summary>
    /// Applies the pending operations to the selected transactions.
    /// </summary>
    public ReactiveCommand<Unit, Unit> ApplyCommand { get; }

    /// <summary>
    /// Commits the dirty transactions to the database.
    /// </summary>
    public ReactiveCommand<Unit, Unit> SaveCommand { get; }

    private readonly SourceCache<TaggedTransaction, string> _transactionsCache = new(x => x.TransactionGuid);
    private readonly ReadOnlyObservableCollection<TaggedTransaction> _transactions;
    public ReadOnlyObservableCollection<TaggedTransaction> Transactions => _transactions;

    public TaggerWindowViewModel(IMediator mediator, IScheduler? scheduler = null)
    {
        _tags = [];
        _searchText = "";
        _mediator = mediator;

        _transactionsCache
            .Connect()
            .Bind(out _transactions)
            .Subscribe();

        _tagsCache
            .Connect()
            .ToSortedCollection(x => x.ToString(), SortDirection.Ascending)
            .ToProperty(this, x => x.Tags, out _tagsHelper);

        this.WhenAnyValue(x => x.SearchText, x => x.StartDate, x => x.EndDate)
            .Throttle(TimeSpan.FromMilliseconds(250), scheduler ?? RxApp.TaskpoolScheduler)
            .Select(tuple => new SearchTransactions(tuple.Item1, tuple.Item2, tuple.Item3))
            .DistinctUntilChanged()
            .Select(req => Observable.FromAsync(ct => _mediator.Send(req, ct)))
            .Switch()
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(OnTransactionsLoaded);

        SelectedTransactions.CollectionChanged += (_, _) => RebuildPendingOperations();

        this.WhenActivated((CompositeDisposable d) =>
        {
            Observable.FromAsync(() => Task.Run(() => _mediator.Send(new FetchTags())))
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(tags =>
                {
                    _log.Information("Found {Count} tags", tags.Count);
                    _tagsCache.Edit(updater =>
                    {
                        updater.Clear();
                        updater.AddOrUpdate(tags);
                    });
                });
        });


        AddTagCommand = ReactiveCommand.Create<Tag, Unit>(OnAddTag);
        AddNewTagCommand = ReactiveCommand.Create<Tag, Unit>(OnAddNewTag);
        CycleOperationCommand = ReactiveCommand.Create<TagOperation, Unit>(OnCycleOperation);
        ApplyCommand = ReactiveCommand.Create(ApplyCommandImpl);
        SaveCommand = ReactiveCommand.CreateFromTask(SaveCommandImpl);
    }

    private Unit OnAddTag(Tag tag)
    {
        var existing = PendingOperations.FirstOrDefault(t => t.Tag == tag);
        if (existing != null)
            existing.Operation = OperationType.Add;
        else
            PendingOperations.Add(new TagOperation { Operation = OperationType.Add, Tag = tag });
        return Unit.Default;
    }

    private Unit OnCycleOperation(TagOperation op)
    {
        op.Operation = op.Operation switch
        {
            OperationType.None => OperationType.Add,
            OperationType.Add => OperationType.Delete,
            OperationType.Delete => OperationType.None,
            _ => OperationType.None
        };
        return Unit.Default;
    }

    private Unit OnAddNewTag(Tag tag)
    {
        if (!_tagsCache.Lookup(tag).HasValue)
        {
            _tagsCache.AddOrUpdate(tag);
        }
        
        var existingOp = PendingOperations.FirstOrDefault(o => o.Tag == tag);
        if (existingOp != null)
            existingOp.Operation = OperationType.Add;
        else
            PendingOperations.Add(new TagOperation { Tag = tag, Operation = OperationType.Add });
        return Unit.Default;
    }

    private void RebuildPendingOperations()
    {
        var allTags = SelectedTransactions.SelectMany(t => t.Tags).Distinct().ToList();
        PendingOperations.Clear();
        foreach (var tag in allTags)
            PendingOperations.Add(new TagOperation { Tag = tag, Operation = OperationType.None });
    }

    private void OnTransactionsLoaded(List<TaggedTransaction> nextTransactions)
    {
        _log.Information("Found {Count} transactions", nextTransactions.Count);

        var cleanTransactions = Transactions.Where(t => !t.IsDirty).Select(t => t.TransactionGuid).ToList();

        var skipCount = 0;
        var addedCount = 0;

        _transactionsCache.Edit(updater =>
        {
            updater.RemoveKeys(cleanTransactions);

            foreach (var t in nextTransactions)
            {
                if (updater.Lookup(t.TransactionGuid).HasValue)
                {
                    skipCount++;
                }
                else
                {
                    updater.AddOrUpdate(t);
                    addedCount++;
                }
            }
        });

        _log.Information("Transactions updated: {Clean} clean removed, {Skipped} already loaded, {Added} added", cleanTransactions.Count, skipCount, addedCount);
    }


    private void ApplyCommandImpl()
    {
        foreach (var txn in SelectedTransactions)
            foreach (var op in PendingOperations)
                op.Apply(txn);
    }

    private async Task SaveCommandImpl(CancellationToken cancellationToken)
    {
        var dirty = Transactions.Where(t => t.IsDirty).ToList();
        await Task.Run(() => _mediator.Send(new ApplyTags(dirty), cancellationToken), cancellationToken);
        foreach (var t in dirty)
            t.IsDirty = false;
    }

    #region design mode

    public TaggerWindowViewModel()
    {
        _mediator = null!;
        _searchText = "";
        AddTagCommand = null!;
        AddNewTagCommand = null!;
        CycleOperationCommand = null!;
        ApplyCommand = null!;
        SaveCommand = null!;
        _tags = [];
        _tagsHelper = null!;

        var txn1 = new TaggedTransaction
        {
            TransactionGuid = "txn-1", Description = "Coffee Shop", Amount = -5.00m,
            Date = new DateOnly(2024, 1, 10), Account = new Account() { FullName = "Expenses:Coffee" }
        };
        txn1.Tags.Add(new Tag("food"));
        txn1.Tags.Add(new Tag("vacation", "disney-2024"));

        var txn2 = new TaggedTransaction
        {
            TransactionGuid = "txn-2", Description = "Gas Station", Amount = -60.00m,
            Date = new DateOnly(2024, 1, 15), Account = new() { FullName = "Expenses:Gas" },
        };
        txn2.Tags.Add(new Tag("travel"));

        var txn3 = new TaggedTransaction
        {
            TransactionGuid = "txn-3", Description = "Grocery Store", Amount = -120.00m,
            Date = new DateOnly(2024, 1, 20), Account = new() { FullName = "Expenses:Groceries" }
        };

        _transactionsCache.AddOrUpdate(txn1);
        _transactionsCache.AddOrUpdate(txn2);
        _transactionsCache.AddOrUpdate(txn3);

        _transactionsCache
                .Connect()
                .Bind(out _transactions)
                .Subscribe();


        _tagsCache.AddOrUpdate([
            new Tag("vacation"),
            new Tag("vacation", "disney-2024"),
            new Tag("vacation", "disney-2025")
        ]);
        
        PendingOperations.Add(new TagOperation { Tag = new Tag("vacation"), Operation = OperationType.None });
        PendingOperations.Add(new TagOperation { Tag = new Tag("vacation", "disney-2024"), Operation = OperationType.Add });
        PendingOperations.Add(new TagOperation { Tag = new Tag("vacation", "disney-2025"), Operation = OperationType.Delete });
    }

    #endregion

    public ViewModelActivator Activator { get; } = new();
}

public partial class TaggedTransaction : ViewModelBase
{
    public string TransactionGuid { get; init; } = "";
    public string Notes { get; set; } = "";
    public DateOnly Date { get; set; }
    public string Description { get; set; } = "";
    public decimal Amount { get; set; }
    public required Account Account { get; init ; }
    public ObservableCollection<Tag> Tags { get; } = [];
    [Reactive] public partial bool IsDirty { get; set; }
    public int? SlotId { get; set; }

    public TaggedTransaction()
    {
        Tags.CollectionChanged += (_, _) => IsDirty = true;
    }
}

public enum OperationType
{
    /// <summary>
    /// Indicates this tag will not be changed in any selected transaction. Transactions that have it already will retain it, and transactions that don't have it will not add it
    /// </summary>
    None,

    /// <summary>
    /// Indicates this tag will be added to all selected transactions.
    /// </summary>
    Add,

    /// <summary>
    /// Indicates this tag will be removed from all selected transactions.
    /// </summary>
    Delete
}

public partial class TagOperation: ViewModelBase
{
    /// <summary>
    /// How this tag will be applied to the selected transactions.
    /// </summary>
    [Reactive] public partial OperationType Operation { get; set; }

    public required Tag Tag { get; init; }

    public void Apply(TaggedTransaction transaction)
    {
        switch (Operation)
        {
            case OperationType.Add:
                if (!transaction.Tags.Contains(Tag))
                    transaction.Tags.Add(Tag);
                break;
            case OperationType.Delete:
                transaction.Tags.Remove(Tag);
                break;
            // None: no-op
        }
    }
}
