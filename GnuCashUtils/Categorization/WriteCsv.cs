using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CsvHelper;
using CsvHelper.Configuration;
using GnuCashUtils.BulkEdit;
using GnuCashUtils.Core;
using MediatR;
using Serilog;

namespace GnuCashUtils.Categorization;

public record WriteCsv(string OutputPath, BankConfig Bank, IReadOnlyList<CategorizationRowViewModel> Transactions): IRequest;

public class WriteCsvHandler : IRequestHandler<WriteCsv>
{
    private static readonly ILogger _log = Log.ForContext<WriteCsvHandler>();
    private readonly IAccountStore _accountStore;

    public WriteCsvHandler(IAccountStore accountStore)
    {
        _accountStore = accountStore;
    }
    
    public async Task Handle(WriteCsv request, CancellationToken cancellationToken)
    {
        var csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            ShouldQuote = _ => true,
        };

        await using var writer = new StreamWriter(request.OutputPath);
        await using var csv = new CsvWriter(writer, csvConfig);

        csv.WriteField("Date");
        csv.WriteField("Transaction ID");
        csv.WriteField("Number");
        csv.WriteField("Description");
        csv.WriteField("Notes");
        csv.WriteField("Commodity/Currency");
        csv.WriteField("Void Reason");
        csv.WriteField("Action");
        csv.WriteField("Memo");
        csv.WriteField("Full Account Name");
        csv.WriteField("Account Name");
        csv.WriteField("Amount With Sym");
        csv.WriteField("Amount Num.");
        csv.WriteField("Value With Sym");
        csv.WriteField("Value Num.");
        csv.WriteField("Reconcile");
        csv.WriteField("Reconcile Date");
        csv.WriteField("Rate/Price");

        await csv.NextRecordAsync();
        
        var bankAccount = _accountStore.Accounts.Items.FirstOrDefault(a => a.FullName == request.Bank.Account);
        if (bankAccount == null)
            throw new Exception($"Could not find an account named '{request.Bank.Account}'");


        foreach (var row in request.Transactions.OrderBy(r => r.CsvIndex))
        {
            Debug.Assert(row.SelectedAccount is not null);

            var transactionId = Guid.NewGuid().ToString("N");
            var bankAmount = request.Bank.SignConvention == SignConvention.Debit ? row.Amount : -row.Amount;
            
            // bank split
            csv.WriteField(row.Date.ToString("yyyy-MM-dd"));
            csv.WriteField(transactionId);
            csv.WriteField("");
            csv.WriteField(row.Description);
            csv.WriteField("");
            csv.WriteField("CURRENCY::USD");
            csv.WriteField("");
            csv.WriteField("");
            csv.WriteField("");
            csv.WriteField(bankAccount.FullName);
            csv.WriteField(bankAccount.Name);
            csv.WriteField(bankAmount.ToString("$#,##0.00;-$#,##0.00"));
            csv.WriteField(bankAmount);
            csv.WriteField(bankAmount.ToString("$#,##0.00;-$#,##0.00"));
            csv.WriteField(bankAmount);
            csv.WriteField("c"); // cleared
            csv.WriteField("");
            csv.WriteField("1.0000");
            await csv.NextRecordAsync();
            
            // transfer split
            var transferAmount = -bankAmount;
            csv.WriteField(row.Date.ToString("yyyy-MM-dd"));
            csv.WriteField(transactionId);
            csv.WriteField("");
            csv.WriteField(row.Description);
            csv.WriteField("");
            csv.WriteField("CURRENCY::USD");
            csv.WriteField("");
            csv.WriteField("");
            csv.WriteField("");
            csv.WriteField(row.SelectedAccount.FullName);
            csv.WriteField(row.SelectedAccount.Name);
            csv.WriteField(transferAmount.ToString("$#,##0.00;-$#,##0.00"));
            csv.WriteField(transferAmount);
            csv.WriteField(transferAmount.ToString("$#,##0.00;-$#,##0.00"));
            csv.WriteField(transferAmount);
            csv.WriteField("n"); // cleared
            csv.WriteField("");
            csv.WriteField("1.0000");
            await csv.NextRecordAsync();
        }

        _log.Information("Saved {TransactionCount} transactions to {OutputPath}", request.Transactions.Count, request.OutputPath);
    }
}