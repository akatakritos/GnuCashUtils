using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
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

    public async Task<List<Account>> Handle(FetchAccountsRequest request, CancellationToken cancellationToken)
    {
        using var connection = _dbConnectionFactory.GetConnection();
        var result = await Task.Run(() => connection.Query<AccountDto>(
            @"with recursive cte as (select a.guid, a.name as full_name, a.name, a.parent_guid
                       from accounts a
                                join accounts p on a.parent_guid = p.guid and p.name = 'Root Account'
                       union
                       select a.guid, concat(cte.full_name, ':', a.name) as full_name, a.name, a.parent_guid
                       from accounts a
                                join cte on a.parent_guid = cte.guid)
select *
from cte order by full_name"));
        var cached = result.AsList();
        var accountByGuid = new Dictionary<string, Account>();
        var accounts = new List<Account>();

        foreach (var dto in cached) // already sorted by full_name, so parents precede children
        {
            accountByGuid.TryGetValue(dto.ParentGuid, out var parent);
            var account = dto.ToAccount(parent);
            parent?.Children.Add(account);
            accountByGuid[dto.Guid] = account;
            accounts.Add(account);
        }

        return accounts;
    }

    private class AccountDto
    {
        public string Guid { get; set; } = "";
        public string Name { get; set; } = "";
        public string ParentGuid { get; set; } = "";
        public string FullName { get; set; } = "";

        public Account ToAccount(Account? parent)
        {
            var account = new Account()
            {
                Parent = parent,
                ParentGuid = parent?.Guid ?? ParentGuid,
                Guid = Guid,
                Children = [],
                FullName = FullName,
                Name = Name,
            };

            return account;
        }
    }
}