using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CsvHelper;
using CsvHelper.Configuration;
using GnuCashUtils.Core;
using MediatR;

namespace GnuCashUtils.Categorization;

public record CsvRow(DateOnly Date, string Description, decimal Amount);

public record ParseCsvRequest(string FilePath, BankConfig Config) : IRequest<List<CsvRow>>;

public class ParseCsvHandler : IRequestHandler<ParseCsvRequest, List<CsvRow>>
{
    public async Task<List<CsvRow>> Handle(ParseCsvRequest request, CancellationToken cancellationToken)
    {
        var mapping = ParseHeadersDsl(request.Config.Headers);
        var rows = new List<CsvRow>();

        using var reader = new StreamReader(request.FilePath);
        var csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = false,
            IgnoreBlankLines = false,
        };
        using var csv = new CsvReader(reader, csvConfig);

        for (int i = 0; i < request.Config.Skip; i++)
            await csv.ReadAsync();

        while (await csv.ReadAsync())
        {
            var dateStr = mapping.DateColumnIndex >= 0 ? csv.GetField(mapping.DateColumnIndex) ?? "" : "";
            var descStr = mapping.DescriptionColumnIndex >= 0 ? csv.GetField(mapping.DescriptionColumnIndex) ?? "" : "";
            var amtStr = mapping.AmountColumnIndex >= 0 ? csv.GetField(mapping.AmountColumnIndex) ?? "" : "";

            if (string.IsNullOrWhiteSpace(dateStr) && string.IsNullOrWhiteSpace(descStr))
                continue;

            var date = DateOnly.ParseExact(dateStr, mapping.DateFormat, CultureInfo.InvariantCulture);
            var amount = decimal.Parse(amtStr, NumberStyles.Any, CultureInfo.InvariantCulture);

            rows.Add(new CsvRow(date, descStr, amount));
        }

        return rows;
    }

    private record ColumnMapping(int DateColumnIndex, string DateFormat, int DescriptionColumnIndex, int AmountColumnIndex);

    private static ColumnMapping ParseHeadersDsl(string headersDsl)
    {
        var parts = headersDsl.Split(',');
        int dateIndex = -1, descIndex = -1, amtIndex = -1;
        string dateFormat = "MM/dd/yyyy";

        for (int i = 0; i < parts.Length; i++)
        {
            var token = parts[i].Trim();
            if (token.StartsWith("{date:") && token.EndsWith("}"))
            {
                dateIndex = i;
                dateFormat = token[6..^1];
            }
            else if (token == "{date}")
            {
                dateIndex = i;
            }
            else if (token == "{description}")
            {
                descIndex = i;
            }
            else if (token == "{amount}")
            {
                amtIndex = i;
            }
        }

        return new ColumnMapping(dateIndex, dateFormat, descIndex, amtIndex);
    }
}