using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using GnuCashUtils.Core;
using MediatR;
namespace GnuCashUtils.BulkEdit;

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
                new { destinationGuid = request.DestinationGuid, splitGuid = transactionViewModel.SplitGuid }, transaction);
        }

        transaction.Commit();
        return Task.CompletedTask;
    }
}