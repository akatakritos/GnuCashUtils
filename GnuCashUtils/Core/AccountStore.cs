using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using DynamicData;
using MediatR;

namespace GnuCashUtils.Core;

public class Account
{
    public Account? Parent { get; init; }
    public string Guid { get; init; } = "";
    public string Name { get; init; } = "";
    public string ParentGuid { get; init; } = "";
    public string FullName { get; init; } = "";
    public List<Account> Children { get; init; } = [];
    
    public override string ToString() => FullName;
}

public interface IAccountStore
{
    SourceCache<Account, string> Accounts { get; }
    SourceCache<Account, string> AccountTree { get; }
    Task Load();
}
public class AccountStore: IAccountStore
{
    private readonly IMediator _mediator;

    public AccountStore(IMediator mediator)
    {
        Debug.WriteLine("Creating AccountStore");
        _mediator = mediator;
    }
    
    public SourceCache<Account, string> Accounts { get; } = new(x => x.Guid);
    public SourceCache<Account, string> AccountTree { get; } = new(x => x.Guid);

    public async Task Load()
    {
        var accounts = await _mediator.Send(new FetchAccountsRequest());
        var tree = accounts.Where(a => a.Parent == null);
        
        Accounts.AddOrUpdate(accounts);
        AccountTree.AddOrUpdate(tree);
    }
}