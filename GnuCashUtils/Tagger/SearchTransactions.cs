using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using DynamicData;
using GnuCashUtils.Core;
using MediatR;
using Serilog;

namespace GnuCashUtils.Tagger;

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
            builder.Where("date(t.post_date) >= date(@startDate)", new { startDate = request.StartDate.Value.ToString("yyyy-MM-dd") });
        if (request.EndDate.HasValue)
            builder.Where("date(t.post_date) <= date(@endDate)", new { endDate = request.EndDate.Value.ToString("yyyy-MM-dd") });

        var selector = builder.AddTemplate(
            @"select t.guid as transaction_guid, t.post_date, a.guid as account_guid, t.description, s.memo, s.value_num, s.value_denom, slots.id as slot_id, slots.string_val as notes
from transactions t
join splits s on s.tx_guid = t.guid
join accounts a on a.guid = s.account_guid and a.account_type = 'EXPENSE'
left join slots on slots.obj_guid = t.guid and slots.name='notes'
/**where**/
order by t.post_date desc
limit 1000
");
        _log.Debug("Query: {Query} {Params:j}", selector.RawSql, request);
        var transactions = connection.Query<Dto>(selector.RawSql, selector.Parameters);
        
        var vms = transactions.Select(t => t.ToViewModel(accountStore)).ToList();
        return Task.FromResult(vms);
    }

    class Dto
    {
        public string TransactionGuid { get; set; } = "";
        public string PostDate { get; set; } = "";
        public string AccountGuid { get; set; } = "";
        public string Description { get; set; } = "";
        public decimal ValueNum { get; set; }
        public decimal ValueDenom { get; set; }
        
        public string? Notes { get; set; }
        public int? SlotId { get; set; }

        public TaggedTransaction ToViewModel(IAccountStore accountStore)
        {
            var txn = new TaggedTransaction()
            {
                TransactionGuid = TransactionGuid,
                Date = DateOnly.FromDateTime(DateTime.Parse(PostDate)),
                Description = Description,
                Amount = ValueNum / ValueDenom,
                Account = accountStore.Accounts.Lookup(AccountGuid).Value,
                Notes = Notes ?? "",
                SlotId = SlotId,
            };

            if (Notes != null)
            {
                txn.Tags.AddRange(Core.Tag.Parse(Notes));
            }

            txn.IsDirty = false;
            return txn;
        }
    }
}