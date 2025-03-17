using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using DynamicData.Binding;
using GnuCashUtils.Core;
using MediatR;
using Microsoft.Data.Sqlite;
using ReactiveUI.SourceGenerators;
using Splat;

namespace GnuCashUtils.BulkEdit;

public class BulkEditWindowViewModel: ViewModelBase
{
    public IObservableCollection<SelectableTransactionViewModel> Transactions { get; } = new ObservableCollectionExtended<SelectableTransactionViewModel>();

    public IObservable<List<Account>> Accounts { get; }

    public BulkEditWindowViewModel(IMediator? mediator = null)
    {
        mediator ??= Locator.Current.GetService<IMediator>();
        Accounts = Observable.FromAsync(() => mediator.Send(new FetchAccountsRequest()));
    }


}

public partial class SelectableTransactionViewModel : ViewModelBase
{
    [Reactive] private string? _description;
    [Reactive] private decimal _amount;
    [Reactive] private DateTime _date;
    [Reactive] private bool _isSelected;
}


public class Account
{
    public string Guid { get; set; }
    public string Name { get; set; }
    public string ParentGuid { get; set; }
    public string FullName { get; set; }
}

public record FetchAccountsRequest() : IRequest<List<Account>>;

public interface IDbConnectionFactory
{
    public SqliteConnection GetConnection();
}

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
from cte");
        return Task.FromResult(result.AsList());
    }
}
