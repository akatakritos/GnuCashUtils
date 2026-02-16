using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using GnuCashUtils.BulkEdit;
using GnuCashUtils.Core;
using MediatR;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Splat;

namespace GnuCashUtils.Categorization;

public partial class CategorizationWindowViewModel : ViewModelBase
{
    [Reactive] public partial string CsvFilePath { get; set; }
    [Reactive] public partial IReadOnlyList<string> Headers { get; set; }
    public ObservableCollection<Account> Accounts { get; } = new();
    public ObservableCollection<CategorizationRowViewModel> Rows { get; } = new();

    public CategorizationWindowViewModel(IMediator? mediator = null)
    {
        CsvFilePath = "";
        Headers = [];

        mediator ??= Locator.Current.GetService<IMediator>();
        if (mediator != null)
        {
            Observable.FromAsync(() => mediator.Send(new FetchAccountsRequest()))
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(accounts =>
                {
                    foreach (var a in accounts)
                        Accounts.Add(a);
                });
        }
    }

    public void LoadCsv(string filePath)
    {
        CsvFilePath = filePath;
        var (headers, rows) = ParseCsv(filePath);
        Headers = headers;
        Rows.Clear();
        foreach (var row in rows)
            Rows.Add(new CategorizationRowViewModel(row, Accounts));
    }

    private static (IReadOnlyList<string> headers, List<string[]> rows) ParseCsv(string filePath)
    {
        var lines = File.ReadAllLines(filePath);

        // Parse every line into fields so we can detect preamble by field count consistency
        var parsedLines = lines.Select(SplitCsvLine).ToArray();
        var fieldCounts = parsedLines.Select(f => f.Length).ToArray();

        // Find the most common field count among lines with >= 2 fields.
        // Lines in the preamble tend to be free-form text with 0 or 1 fields.
        var mode = fieldCounts
            .Where(c => c >= 2)
            .GroupBy(c => c)
            .OrderByDescending(g => g.Count())
            .FirstOrDefault()?.Key ?? 1;

        // The first line matching the modal field count is the header row
        var startIndex = Array.FindIndex(fieldCounts, c => c == mode);
        if (startIndex < 0) startIndex = 0;

        var dataLines = parsedLines[startIndex..];
        if (dataLines.Length == 0)
            return ([], []);

        var headers = dataLines[0];
        var rows = dataLines[1..]
            .Where(r => r.Any(f => !string.IsNullOrWhiteSpace(f))) // skip blank trailing rows
            .ToList();

        return (headers, rows);
    }

    private static string[] SplitCsvLine(string line)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++; // skip escaped quote
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    current.Append(c);
                }
            }
            else
            {
                if (c == '"')
                {
                    inQuotes = true;
                }
                else if (c == ',')
                {
                    fields.Add(current.ToString().Trim());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }
        }

        fields.Add(current.ToString().Trim());
        return [.. fields];
    }
}

public partial class CategorizationRowViewModel : ViewModelBase
{
    public string[] CsvFields { get; }
    public ObservableCollection<Account> Accounts { get; }
    [Reactive] public partial string Merchant { get; set; }
    [Reactive] public partial Account? SelectedAccount { get; set; }

    public CategorizationRowViewModel(string[] csvFields, ObservableCollection<Account> accounts)
    {
        CsvFields = csvFields;
        Accounts = accounts;
        Merchant = "";
    }
}
