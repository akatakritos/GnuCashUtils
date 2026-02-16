using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Reactive;
using System.Reactive.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CsvHelper;
using CsvHelper.Configuration;
using GnuCashUtils.BulkEdit;
using GnuCashUtils.Core;
using MediatR;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Splat;
using Unit = System.Reactive.Unit;

namespace GnuCashUtils.Categorization;

public partial class CategorizationWindowViewModel : ViewModelBase
{
    private readonly IConfigService _configService;

    [Reactive] public partial string CsvFilePath { get; set; }
    public ObservableCollection<Account> Accounts { get; } = new();
    public ObservableCollection<CategorizationRowViewModel> Rows { get; } = new();
    public Interaction<string, Unit> ShowError { get; } = new();

    public CategorizationWindowViewModel(IMediator? mediator = null, IConfigService? configService = null)
    {
        CsvFilePath = "";
        _configService = configService ?? Locator.Current.GetService<IConfigService>() ?? new ConfigService();

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

    public async Task LoadCsv(string filePath)
    {
        CsvFilePath = filePath;

        var fileName = Path.GetFileName(filePath);
        BankConfig? bankConfig = null;
        foreach (var bank in _configService.CurrentConfig.Banks)
        {
            if (Regex.IsMatch(fileName, bank.Match))
            {
                bankConfig = bank;
                break;
            }
        }

        if (bankConfig is null)
        {
            await ShowError.Handle($"No bank configuration matched '{fileName}'. Check config.yml.");
            return;
        }

        var mapping = ParseHeadersDsl(bankConfig.Headers);

        var rows = new List<CategorizationRowViewModel>();
        using var reader = new StreamReader(filePath);
        var csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = false,
            IgnoreBlankLines = false,
        };
        using var csv = new CsvReader(reader, csvConfig);

        for (int i = 0; i < bankConfig.Skip; i++)
            csv.Read();

        while (csv.Read())
        {
            var dateStr = mapping.DateColumnIndex >= 0 ? csv.GetField(mapping.DateColumnIndex) ?? "" : "";
            var descStr = mapping.DescriptionColumnIndex >= 0 ? csv.GetField(mapping.DescriptionColumnIndex) ?? "" : "";
            var amtStr = mapping.AmountColumnIndex >= 0 ? csv.GetField(mapping.AmountColumnIndex) ?? "" : "";

            if (string.IsNullOrWhiteSpace(dateStr) && string.IsNullOrWhiteSpace(descStr))
                continue;

            var date = DateOnly.ParseExact(dateStr, mapping.DateFormat, CultureInfo.InvariantCulture);
            var amount = decimal.Parse(amtStr, NumberStyles.Any, CultureInfo.InvariantCulture);

            rows.Add(new CategorizationRowViewModel(date, descStr, amount, Accounts));
        }

        Rows.Clear();
        foreach (var row in rows)
            Rows.Add(row);
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

public partial class CategorizationRowViewModel : ViewModelBase
{
    [Reactive] public partial DateOnly Date { get; set; }
    [Reactive] public partial string Description { get; set; }
    [Reactive] public partial decimal Amount { get; set; }
    public ObservableCollection<Account> Accounts { get; }
    [Reactive] public partial string Merchant { get; set; }
    [Reactive] public partial Account? SelectedAccount { get; set; }

    public CategorizationRowViewModel(DateOnly date, string description, decimal amount, ObservableCollection<Account> accounts)
    {
        Date = date;
        Description = description;
        Amount = amount;
        Accounts = accounts;
        Merchant = "";
    }
}
