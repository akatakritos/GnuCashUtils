using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using DynamicData;
using GnuCashUtils.Core;
using MediatR;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Serilog;
using Unit = System.Reactive.Unit;

namespace GnuCashUtils.Tagger;

// TODO: ability to add brand new tag to list
// TODO: shouldn't have to type "#[" to get auto-complete
// TODO: search should include tag
// TODO: what to do if there are unsaved transactions and a new search is made?
// TODO: Support multiple selected transactions. Tags in the listbox need three states: ignore, remove, add. When selecting one or more rows, merge all tags into the listbox, but have them all at ignore. User can mark one to remove, an icon indicates its being removed. User can add a new one, icon indicates its being added. A tag that was in one transaction (default ignored) can be marked to be applied to all (added)
// TODO: auto-complete dropdown still not disappearing on selection
// TODO: make it prettier

public partial class TaggerWindowViewModel : ViewModelBase, IActivatableViewModel
{
    private static readonly ILogger _log = Log.ForContext<TaggerWindowViewModel>();

    private readonly IMediator _mediator;
    public ObservableCollection<TaggedTransaction> Transactions { get; } = [];
    public ObservableCollection<Tag> Tags { get; } = [];

    [Reactive] public partial string SearchText { get; set; }
    [Reactive] public partial DateOnly? StartDate { get; set; }
    [Reactive] public partial DateOnly? EndDate { get; set; }
    [Reactive] public partial TaggedTransaction? SelectedTransaction { get; set; }

    public ObservableCollection<Tag> SelectedTags { get; } = [];
    public ReactiveCommand<Tag, Unit> AddTagCommand { get; }
    public ReactiveCommand<TaggedTransaction?, Unit> SelectTransactionCommand { get; }
    public ReactiveCommand<Unit, Unit> ApplyCommand { get; }
    public ReactiveCommand<Unit, Unit> SaveCommand { get; }

    public TaggerWindowViewModel(IMediator mediator, IScheduler? scheduler = null)
    {
        _searchText = "";
        _mediator = mediator;

        this.WhenAnyValue(x => x.SearchText, x => x.StartDate, x => x.EndDate)
            .Throttle(TimeSpan.FromMilliseconds(250), scheduler ?? RxApp.TaskpoolScheduler)
            .Select(tuple => new SearchTransactions(tuple.Item1, tuple.Item2, tuple.Item3))
            .DistinctUntilChanged()
            .Select(req => Observable.FromAsync(ct => mediator.Send(req, ct)))
            .Switch()
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(transactions =>
            {
                _log.Information("Found {Count} transactions", transactions.Count);
                Transactions.Clear();
                Transactions.AddRange(transactions);
            });

        this.WhenActivated((CompositeDisposable d) =>
        {
            Observable.FromAsync(() => Task.Run(() => mediator.Send(new FetchTags())))
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(tags =>
                {
                    _log.Information("Found {Count} tags", tags.Count);
                    Tags.Clear();
                    Tags.AddRange(tags.OrderBy(t => t.Name).ThenBy(t => t.Value, StringComparer.Ordinal));
                });
        });


        AddTagCommand = ReactiveCommand.Create<Tag, Unit>(tag =>
        {
            if (!SelectedTags.Contains(tag))
                SelectedTags.Add(tag);
            return Unit.Default;
        });

        SelectTransactionCommand = ReactiveCommand.Create<TaggedTransaction?, Unit>(SelectTransactionImpl);
        this.WhenAnyValue(x => x.SelectedTransaction).InvokeCommand(SelectTransactionCommand);

        ApplyCommand = ReactiveCommand.Create(ApplyCommandImpl);
        SaveCommand = ReactiveCommand.CreateFromTask(SaveCommandImpl);
    }

    private Unit SelectTransactionImpl(TaggedTransaction? transaction)
    {
        SelectedTags.Clear();
        if (transaction != null)
            SelectedTags.AddRange(transaction.Tags);
        return Unit.Default;
    }

    private void ApplyCommandImpl()
    {
        if (SelectedTransaction != null)
        {
            SelectedTransaction.Tags.Clear();
            SelectedTransaction.Tags.AddRange(SelectedTags);
        }
    }

    private async Task SaveCommandImpl(CancellationToken cancellationToken)
    {
        var dirty = Transactions.Where(t => t.IsDirty).ToList();
        await Task.Run(() => _mediator.Send(new ApplyTags(dirty), cancellationToken), cancellationToken);
        foreach (var t in dirty)
            t.IsDirty = false;
    }

    #region mode

    public TaggerWindowViewModel()
    {
        _mediator = null!;
        _searchText = "";
        AddTagCommand = null!;
        SelectTransactionCommand = null!;
        ApplyCommand = null!;
        SaveCommand = null!;

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

        Transactions = [txn1, txn2, txn3];

        Tags =
        [
            new Tag("vacation"),
            new Tag("vacation", "disney-2024"),
            new Tag("vacation", "disney-2025")
        ];
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
