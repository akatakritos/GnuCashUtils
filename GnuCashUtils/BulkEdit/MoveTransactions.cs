using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using GnuCashUtils.Core;
using MediatR;
using Serilog;

namespace GnuCashUtils.BulkEdit;

public record MoveTransactionsCommand(
    IEnumerable<TransactionViewModel> Transactions,
    string SourceGuid,
    string DestinationGuid) : IRequest;

public class MoveTransactionsHandler : IRequestHandler<MoveTransactionsCommand>
{
    private static readonly ILogger _log = Log.ForContext<MoveTransactionsHandler>();
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
        var count = 0;
        foreach (var transactionViewModel in request.Transactions)
        {
            connection.Execute(
                @"update splits set account_guid = @destinationGuid where guid = @splitGuid",
                new { destinationGuid = request.DestinationGuid, splitGuid = transactionViewModel.SplitGuid }, transaction);
            _log.Debug("Moved {SplitGuid} to {DestinationGuid}", transactionViewModel.SplitGuid, request.DestinationGuid);
            count++;
        }

        transaction.Commit();
        _log.Information("Moved {Count} transactions", count);
        return Task.CompletedTask;
    }
}