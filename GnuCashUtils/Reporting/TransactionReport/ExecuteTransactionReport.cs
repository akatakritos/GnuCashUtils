using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using GnuCashUtils.Core;
using GnuCashUtils.Tagger;
using HandlebarsDotNet;
using MediatR;
using Tag = GnuCashUtils.Core.Tag;

namespace GnuCashUtils.Reporting.TransactionReport;

public record ExecuteTransactionReport(Tag? Tag = null) : IRequest;

public class ExecuteTransactionReportHandler(IDbConnectionFactory db, IAccountStore accountStore)
    : IRequestHandler<ExecuteTransactionReport>
{
    public Task Handle(ExecuteTransactionReport request, CancellationToken cancellationToken)
    {
        var data = BuildReportData(request);

        var templatePath = TemplateResolver.Resolve("TransactionReportTemplate.html.hbs");
        var templateSource = File.ReadAllText(templatePath);
        var template = Handlebars.Compile(templateSource);
        var html = template(data);

        var tempFile = Path.Combine(Path.GetTempPath(), $"TransactionReport_{Guid.NewGuid():N}.html");
        File.WriteAllText(tempFile, html);
        Process.Start(new ProcessStartInfo(tempFile) { UseShellExecute = true });

        return Task.CompletedTask;
    }

    public TransactionReportData BuildReportData(ExecuteTransactionReport request)
    {
        var rows = GetTransactions(request);

        var groups = rows
            .GroupBy(r => r.AccountGuid)
            .Select(g =>
            {
                var account = accountStore.Accounts.Lookup(g.Key).Value;
                decimal runningBalance = 0;
                var txns = g.Select(r =>
                {
                    var amount = r.ValueNum / r.ValueDenom;
                    runningBalance += amount;
                    return new TransactionReportTransaction
                    {
                        Date = DateOnly.FromDateTime(DateTime.Parse(r.PostDate)),
                        Description = r.Description,
                        Amount = amount,
                        Account = account,
                        RunningBalance = runningBalance,
                    };
                }).ToList();
                return new TransactionReportAccountGroup
                {
                    Account = account,
                    Transactions = txns,
                };
            })
            .OrderBy(g => g.Account.FullName)
            .ToList();

        var filters = new List<ReportFilter>();
        if (request.Tag != null)
        {
            var displayText = request.Tag.Value != null
                ? $"{request.Tag.Name}={request.Tag.Value}"
                : request.Tag.Name;
            filters.Add(new ReportFilter("Tag", displayText, TagChipColor(request.Tag.Name)));
        }

        return new TransactionReportData { AccountGroups = groups, Filters = filters };
    }

    // Mirrors the deterministic palette in Tagger/Converters.cs
    private static readonly string[] _tagColors =
    [
        "#1976D2", "#43A047", "#AD1457", "#6A1B9A",
        "#F57C00", "#00838F", "#C62828", "#37474F",
    ];

    private static string TagChipColor(string tagName)
    {
        var hash = tagName.Aggregate(0, (h, c) => h * 31 + c);
        return _tagColors[Math.Abs(hash) % _tagColors.Length];
    }

    protected virtual List<Dto> GetTransactions(ExecuteTransactionReport request)
    {
        using var connection = db.GetConnection();
        var builder = new SqlBuilder();

        if (request.Tag != null)
            builder.Where("slots.string_val LIKE @tagPattern", new { tagPattern = $"%{request.Tag.Encode()}%" });

        var selector = builder.AddTemplate(
            @"select t.guid as transaction_guid, t.post_date, a.guid as account_guid, t.description, s.value_num, s.value_denom
from transactions t
join splits s on s.tx_guid = t.guid
join accounts a on a.guid = s.account_guid and a.account_type = 'EXPENSE'
left join slots on slots.obj_guid = t.guid and slots.name='notes'
/**where**/
order by a.name, t.post_date");

        return connection.Query<Dto>(selector.RawSql, selector.Parameters).ToList();
    }

    public class Dto
    {
        public string TransactionGuid { get; set; } = "";
        public string PostDate { get; set; } = "";
        public string AccountGuid { get; set; } = "";
        public string Description { get; set; } = "";
        public decimal ValueNum { get; set; }
        public decimal ValueDenom { get; set; }
    }
}

public record ReportFilter(string Name, string Value, string? ChipColor = null);

public class TransactionReportData
{
    public required IReadOnlyList<TransactionReportAccountGroup> AccountGroups { get; init; }
    public IReadOnlyList<ReportFilter> Filters { get; init; } = [];
    public decimal GrandTotal => AccountGroups.Sum(g => g.Total);
    public string FormattedGrandTotal => GrandTotal.ToString("C");
}

public class TransactionReportAccountGroup
{
    public required IReadOnlyList<TransactionReportTransaction> Transactions { get; init; }
    public required Account Account { get; init; }
    public decimal Total => Transactions.Sum(t => t.Amount);
    public string FormattedTotal => Total.ToString("C");
}

public class TransactionReportTransaction
{
    public required DateOnly Date { get; init; }
    public required string Description { get; init; }
    public required decimal Amount { get; init; }
    public required Account Account { get; init; }
    public decimal RunningBalance { get; init; }

    public string FormattedDate => Date.ToString("yyyy-MM-dd");
    public string FormattedAmount => Amount.ToString("C");
    public string FormattedRunningBalance => RunningBalance.ToString("C");
}
