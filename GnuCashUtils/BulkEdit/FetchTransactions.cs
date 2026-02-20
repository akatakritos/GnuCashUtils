using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using GnuCashUtils.Core;
using MediatR;

namespace GnuCashUtils.BulkEdit;

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
