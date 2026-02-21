using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using DynamicData;
using GnuCashUtils.Core;
using MediatR;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Serilog;
using Unit = System.Reactive.Unit;

namespace GnuCashUtils.Tagger;

public partial class TaggerWindowViewModel : ViewModelBase
{
    private static readonly ILogger _log = Log.ForContext<TaggerWindowViewModel>();

    private readonly IMediator _mediator;
    public ObservableCollection<TaggedTransaction> Transactions { get; } = [];
    public ObservableCollection<Tag> Tags { get; } = [];

    [Reactive] public partial string SearchText { get; set; }
    [Reactive] public partial DateOnly? StartDate { get; set; }
    [Reactive] public partial DateOnly? EndDate { get; set; }

    public ObservableCollection<Tag> SelectedTags { get; } = [];
    public ReactiveCommand<IEnumerable<TaggedTransaction>, Unit> ApplyCommand { get; }
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


        ApplyCommand = ReactiveCommand.Create<IEnumerable<TaggedTransaction>, Unit>(ApplyCommandImpl);
        SaveCommand = ReactiveCommand.CreateFromTask(SaveCommandImpl);
    }

    private Unit ApplyCommandImpl(IEnumerable<TaggedTransaction> transactions)
    {
        foreach (var transaction in transactions)
        {
            transaction.Tags.Clear();
            transaction.Tags.AddRange(SelectedTags);
        }

        return Unit.Default;
    }

    private async Task SaveCommandImpl()
    {
        await _mediator.Send(new ApplyTags(Transactions.Where(t => t.IsDirty)));
    }

    #region mode

    public TaggerWindowViewModel()
    {
        _mediator = null!;
        _searchText = "";
        ApplyCommand = null!;
        SaveCommand = null!;

        Transactions =
        [
            new()
            {
                TransactionGuid = "txn-1", Description = "Coffee Shop", Amount = -5.00m,
                Date = new DateOnly(2024, 1, 10), Account = new Account() { FullName = "Expenses:Coffee" }
            },
            new()
            {
                TransactionGuid = "txn-2", Description = "Gas Station", Amount = -60.00m,
                Date = new DateOnly(2024, 1, 15), Account = new() { FullName = "Expenses:Gas" },
            },
            new()
            {
                TransactionGuid = "txn-3", Description = "Grocery Store", Amount = -120.00m,
                Date = new DateOnly(2024, 1, 20), Account = new() { FullName = "Expenses:Groceries" }
            },
        ];

        Tags =
        [
            new Tag("vacation"),
            new Tag("vacation", "disney-2024"),
            new Tag("vacation", "disney-2025")
        ];
    }

    #endregion
}

public partial class TaggedTransaction : ViewModelBase
{
    public string TransactionGuid { get; init; } = "";
    public string Notes { get; set; } = "";
    public DateOnly Date { get; set; }
    public string Description { get; set; } = "";
    public decimal Amount { get; set; }
    public Account Account { get; set; }
    public ObservableCollection<Tag> Tags { get; } = [];
    [Reactive] public bool IsDirty { get; set; }

    public TaggedTransaction()
    {
        Tags.CollectionChanged += (_, _) => IsDirty = true;
    }
}

public record SearchTransactions(string SearchText, DateOnly? StartDate, DateOnly? EndDate)
    : IRequest<List<TaggedTransaction>>;

public class SearchTransactionsHandler(IDbConnectionFactory db, IAccountStore accountStore)
    : IRequestHandler<SearchTransactions, List<TaggedTransaction>>
{
    private static readonly ILogger _log = Log.ForContext<SearchTransactionsHandler>();
    public Task<List<TaggedTransaction>> Handle(SearchTransactions request, CancellationToken cancellationToken)
    {
        using var connection = db.GetConnection();
        var builder = new SqlBuilder();

        if (!string.IsNullOrWhiteSpace(request.SearchText))
            builder.Where("t.description like @searchText", new { searchText = $"%{request.SearchText}%" });
        if (request.StartDate.HasValue)
            builder.Where("t.post_date >= @startDate", new { startDate = request.StartDate.Value });
        if (request.EndDate.HasValue)
            builder.Where("t.post_date <= @endDate", new { endDate = request.EndDate.Value });

        var selector = builder.AddTemplate(
            @"select t.guid as transaction_guid, t.post_date, a.guid as account_guid, t.description, s.memo, s.value_num, s.value_denom, slots.string_val as notes
from transactions t
join splits s on s.tx_guid = t.guid
join accounts a on a.guid = s.account_guid and a.account_type = 'EXPENSE'
left join slots on slots.obj_guid = t.guid and slots.name='notes'
/**where**/
order by t.post_date desc
limit 1000
");
        _log.Debug("Query: {Query}", selector.RawSql);
        var transactions = connection.Query<Dto>(selector.RawSql, selector.Parameters);
        
        var vms = transactions.Select(t => t.ToViewModel(accountStore)).ToList();
        return Task.FromResult(vms);
    }

    class Dto
    {
        public string TransactionGuid { get; set; }
        public string PostDate { get; set; }
        public string AccountGuid { get; set; }
        public string Description { get; set; }
        public decimal ValueNum { get; set; }
        public decimal ValueDenom { get; set; }
        public string? Notes { get; set; }

        public TaggedTransaction ToViewModel(IAccountStore accountStore)
        {
            return new TaggedTransaction()
            {
                TransactionGuid = TransactionGuid,
                Date = DateOnly.FromDateTime(DateTime.Parse(PostDate)),
                Description = Description,
                Amount = ValueNum / ValueDenom,
                Account = accountStore.Accounts.Lookup(AccountGuid).Value,
                Notes = Notes ?? "",
            };
        }
    }
}

public record FetchTags() : IRequest<List<Tag>>;

public class FetchTagsHandler(IDbConnectionFactory db) : IRequestHandler<FetchTags, List<Tag>>
{
    public Task<List<Tag>> Handle(FetchTags request, CancellationToken cancellationToken)
    {
        return Task.FromResult(new List<Tag>());
    }
}

public record ApplyTags(IEnumerable<TaggedTransaction> Transactions) : IRequest;

public class ApplyTagsHandler(IDbConnectionFactory db) : IRequestHandler<ApplyTags>
{
    public Task Handle(ApplyTags request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}