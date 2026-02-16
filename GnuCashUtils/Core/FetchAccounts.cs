using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using GnuCashUtils.BulkEdit;
using MediatR;

namespace GnuCashUtils.Core;

public record FetchAccountsRequest() : IRequest<List<Account>>;

public class FetchAccounts : IRequestHandler<FetchAccountsRequest, List<Account>>
{
    private readonly IDbConnectionFactory _dbConnectionFactory;

    public FetchAccounts(IDbConnectionFactory dbConnectionFactory)
    {
        _dbConnectionFactory = dbConnectionFactory;
    }

    public Task<List<Account>> Handle(FetchAccountsRequest request, CancellationToken cancellationToken)
    {
        using var connection = _dbConnectionFactory.GetConnection();
        var result = connection.Query<Account>(@"with recursive cte as (select a.guid, a.name as full_name, a.name, a.parent_guid
                       from accounts a
                                join accounts p on a.parent_guid = p.guid and p.name = 'Root Account'
                       union
                       select a.guid, concat(cte.name, ':', a.name) as full_name, a.name, a.parent_guid
                       from accounts a
                                join cte on a.parent_guid = cte.guid)
select *
from cte order by name");
        return Task.FromResult(result.AsList());
    }
}