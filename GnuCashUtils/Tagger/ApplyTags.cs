using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using GnuCashUtils.Core;
using MediatR;

namespace GnuCashUtils.Tagger;

public record ApplyTags(IEnumerable<TaggedTransaction> Transactions) : IRequest;

public class ApplyTagsHandler(IDbConnectionFactory db) : IRequestHandler<ApplyTags>
{
    public Task Handle(ApplyTags request, CancellationToken cancellationToken)
    {
        using var conn = db.GetConnection();
        using var transaction = conn.BeginTransaction();

        foreach (var taggedTransaction in request.Transactions)
        {
            var notes = taggedTransaction.ComputeNotes();

            if (taggedTransaction.SlotId.HasValue)
            {
                conn.Execute("update slots set string_val = @notes where id = @slotId", new { notes, slotId = taggedTransaction.SlotId.Value }, transaction);
            }
            else
            {
                conn.Execute(@"
                    insert into slots (obj_guid, name, slot_type, int64_val, string_val, timespec_val, numeric_val_num, numeric_val_denom)
                    values (@transactionGuid, 'notes', 4, 0, @notes, '1970-01-01 00:00:00', 0, 1)",
                    new
                    {
                        transactionGuid = taggedTransaction.TransactionGuid,
                        notes
                    }, transaction);

            }
        }
        
        transaction.Commit();
        return Task.CompletedTask;
    }
}
