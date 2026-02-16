using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using DynamicData;
using GnuCashUtils.Core;
using MediatR;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Splat;
using Unit = System.Reactive.Unit;


namespace GnuCashUtils.BulkEdit;

public partial class BulkEditWindowViewModel : ViewModelBase
{
    [Reactive] public partial Account? SourceAccount { get; set; }
    [Reactive] public partial Account? DestinationAccount { get; set; }
    [Reactive] public partial string SearchText { get; set; }
    private readonly Subject<Account> _refreshRequested = new();
    
    public ReactiveCommand<Unit, Unit> MoveCommand { get; }
    public ReactiveCommand<Unit, Unit> SelectAllCommand { get; }
    
    public IObservable<List<Account>> Accounts { get; }
    public ObservableCollection<SelectableTransactionViewModel> Transactions { get; } = new();
    
    public IObservable<int> TransactionCount { get; }
    public IObservable<int> FilteredTransactionCount { get; }
    public IObservable<int> SelectedTransactionCount { get; }

    public BulkEditWindowViewModel(IMediator? mediator = null)
    {
        mediator ??= Locator.Current.GetService<IMediator>()!;
        SearchText = "";
        Accounts = Observable.FromAsync(() => mediator.Send(new FetchAccountsRequest()));
        

        var accountTransactions = this.WhenAnyValue(x => x.SourceAccount)
            .Merge(_refreshRequested)
            .Where(x => x != null)
            .Select(x => x!.Guid)
            .Throttle(TimeSpan.FromMilliseconds(250))
            .DistinctUntilChanged()
            .Select(accountGuid =>
                Observable.FromAsync((ct) => Task.Run(() => mediator.Send(new FetchTransactions(accountGuid), ct), ct)))
            .Switch()
            .Publish();
        accountTransactions.Connect();


        var searchText = this.WhenAnyValue(x => x.SearchText);

        accountTransactions
            .CombineLatest(searchText)
            .Select(x => x.Item1.Where(t => t.Description?.Contains(x.Item2, StringComparison.OrdinalIgnoreCase) == true))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(x =>
            {
                Transactions.Clear();
                Transactions.AddRange(x);
            });
        
        
        TransactionCount = accountTransactions.Select(t => t.Count);
        FilteredTransactionCount = this.WhenAnyValue(x => x.Transactions)
            .Select(t => t.Count);
        SelectedTransactionCount = this.WhenAnyValue(x => x.Transactions)
            .Select(t => t.Count(x => x.IsSelected));

        SelectAllCommand = ReactiveCommand.Create(
            execute: () =>
            {
                foreach (var t in Transactions)
                {
                    t.IsSelected = true;
                }
            },
            canExecute: TransactionCount.Select(n => n > 0));


        MoveCommand = ReactiveCommand.CreateFromTask(async () =>
            {
                if (SourceAccount == null || DestinationAccount == null) return;
                
                await mediator.Send(new MoveTransactionsCommand(Transactions, SourceAccount.Guid,
                    DestinationAccount.Guid));
                _refreshRequested.OnNext(SourceAccount);
            }
        );
    }
}

public record MoveTransactionsCommand(
    IEnumerable<SelectableTransactionViewModel> Transactions,
    string SourceGuid,
    string DestinationGuid) : IRequest;

public class MoveTransactionsHandler : IRequestHandler<MoveTransactionsCommand>
{
    private readonly IDbConnectionFactory _dbConnectionFactory;

    public MoveTransactionsHandler(IDbConnectionFactory dbConnectionFactory)
    {
        _dbConnectionFactory = dbConnectionFactory;
    }

    public Task Handle(MoveTransactionsCommand request, CancellationToken cancellationToken)
    {
        using var connection = _dbConnectionFactory.GetConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        foreach (var transactionViewModel in request.Transactions.Where(t => t.IsSelected))
        {
            connection.Execute(
                @"update splits set account_guid = @destinationGuid where guid = @splitGuid",
                new { destinationGuid = request.DestinationGuid, splitGuid = transactionViewModel.SplitGuid });
        }

        transaction.Commit();
        return Task.CompletedTask;
    }
}

public partial class SelectableTransactionViewModel : ViewModelBase
{
    [Reactive] private string? _description;
    [Reactive] private decimal _amount;
    [Reactive] private DateTime _date;
    [Reactive] private bool _isSelected;
    [Reactive] private string _transactionGuid = "";
    [Reactive] private string? _splitGuid;
    [Reactive] private string? _accountGuid;
}

public class Account
{
    public string Guid { get; set; } = "";
    public string Name { get; set; } = "";
    public string ParentGuid { get; set; } = "";
    public string FullName { get; set; } = "";
}

public record FetchAccountsRequest() : IRequest<List<Account>>;

public class FetchAccountsHandler : IRequestHandler<FetchAccountsRequest, List<Account>>
{
    private readonly IDbConnectionFactory _dbConnectionFactory;

    public FetchAccountsHandler(IDbConnectionFactory dbConnectionFactory)
    {
        _dbConnectionFactory = dbConnectionFactory;
    }

    public Task<List<Account>> Handle(FetchAccountsRequest request, CancellationToken cancellationToken)
    {
        using var connection = _dbConnectionFactory.GetConnection();
        var result = connection.Query<Account>(@"with recursive cte as (select a.guid, a.name, a.parent_guid
                       from accounts a
                                join accounts p on a.parent_guid = p.guid and p.name = 'Root Account'
                       union
                       select a.guid, concat(cte.name, ':', a.name) as name, a.parent_guid
                       from accounts a
                                join cte on a.parent_guid = cte.guid)
select *
from cte order by name");
        return Task.FromResult(result.AsList());
    }
}

public record FetchTransactions(string AccountGuid) : IRequest<List<SelectableTransactionViewModel>>;

public class FetchTransactionsHandler : IRequestHandler<FetchTransactions, List<SelectableTransactionViewModel>>
{
    private readonly IDbConnectionFactory _dbConnectionFactory;

    public FetchTransactionsHandler(IDbConnectionFactory dbConnectionFactory)
    {
        _dbConnectionFactory = dbConnectionFactory;
    }

    public Task<List<SelectableTransactionViewModel>> Handle(FetchTransactions request,
        CancellationToken cancellationToken)
    {
        using var connection = _dbConnectionFactory.GetConnection();
        cancellationToken.ThrowIfCancellationRequested();
        Console.WriteLine("query " + request.AccountGuid);
        var result = connection.Query<Dto>(
            @"select t.post_date as date, t.guid as transaction_guid, t.description, s.value_num, s.value_denom, s.guid as split_guid  from transactions t
join splits s on s.tx_guid = t.guid
join accounts a on s.account_guid = a.guid
where a.guid = @accountGuid
order by t.post_date desc", new { accountGuid = request.AccountGuid });

        cancellationToken.ThrowIfCancellationRequested();
        var converted = result.Select(r => new SelectableTransactionViewModel()
        {
            Date = r.Date,
            Description = r.Description,
            Amount = r.ValueNum / (decimal)r.ValueDenom,
            TransactionGuid = r.TransactionGuid,
            SplitGuid = r.SplitGuid,
        }).ToList();

        return Task.FromResult(converted);
    }

    public class Dto
    {
        public DateTime Date { get; set; }
        public string TransactionGuid { get; set; } = "";
        public string Description { get; set; } = "";
        public long ValueNum { get; set; }
        public long ValueDenom { get; set; }
        public string SplitGuid { get; set; } = "";
    }
}