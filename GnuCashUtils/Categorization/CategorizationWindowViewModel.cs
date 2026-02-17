using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CsvHelper;
using CsvHelper.Configuration;
using DynamicData;
using GnuCashUtils.Core;
using MediatR;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Splat;
using Unit = System.Reactive.Unit;
using DynamicData.Aggregation;

namespace GnuCashUtils.Categorization;

public partial class CategorizationWindowViewModel : ViewModelBase, IActivatableViewModel
{
    private readonly IMediator? _mediator;
    private readonly IConfigService _configService;
    private bool _amountNegated;

    [Reactive] public partial string CsvFilePath { get; set; }
    [Reactive] public partial string StatusMessage { get; set; }
    [Reactive] public partial bool ShowOnlyErrors { get; set; }
    public Interaction<string, Unit> ShowError { get; } = new();

    private readonly SourceCache<CategorizationRowViewModel, int> _source = new(row => row.CsvIndex);
    private readonly ReadOnlyObservableCollection<CategorizationRowViewModel> _filteredRows;
    public ReadOnlyObservableCollection<CategorizationRowViewModel> Rows => _filteredRows;
    
    [ObservableAsProperty] public partial int InvalidCount { get; }
    public ReactiveCommand<Unit, Unit> SaveCommand { get; }
    
    [ObservableAsProperty] public partial IReadOnlyCollection<Account> Accounts { get; }
    [ObservableAsProperty] public partial IReadOnlyCollection<Account> AccountTree { get; }

    public CategorizationWindowViewModel(IMediator? mediator = null, IConfigService? configService = null,
        IAccountStore? store = null)
    {
        CsvFilePath = "";
        StatusMessage = "";
        _configService = configService ?? Locator.Current.GetService<IConfigService>() ?? new ConfigService();
        _mediator = mediator ?? Locator.Current.GetService<IMediator>()!;
        store ??= Locator.Current.GetService<IAccountStore>()!;

        store.Accounts
            .Connect()
            .ObserveOn(RxApp.MainThreadScheduler)
            .ToSortedCollection(x => x.FullName)
            .ToProperty(this, x => x.Accounts, out _accountsHelper);
        
        store.AccountTree
            .Connect()
            .ObserveOn(RxApp.MainThreadScheduler)
            .ToSortedCollection(x => x.FullName)
            .ToProperty(this, x => x.AccountTree, out _accountTreeHelper);

        this.WhenActivated(d =>
        {
            _accountsHelper.DisposeWith(d);
            _accountsHelper.DisposeWith(d);
        });


        var filter = this.WhenAnyValue(x => x.ShowOnlyErrors)
            .Select(showOnlyErrors => showOnlyErrors ? new Func<CategorizationRowViewModel, bool>(row => !row.IsValid) : _ => true);

        _source.Connect()
            .Filter(filter)
            .Sort(Comparer<CategorizationRowViewModel>.Create((a, b) => a.CsvIndex.CompareTo(b.CsvIndex)))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Bind(out _filteredRows)
            .Subscribe();

        var errorCount =_source.Connect()
            .AutoRefresh(x => x.IsValid)
            .Filter(row => !row.IsValid)
            .Count()
            .Publish()
            .RefCount();
            
        errorCount.ToProperty(this, x => x.InvalidCount, out _invalidCountHelper);

        SaveCommand = ReactiveCommand.CreateFromTask(
            Save,
            canExecute: errorCount.Select(n => n == 0));


        _configService.Config
            .Skip(1)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(config =>
            {
                var matcher = new MerchantMatcher(config.Merchants);
                foreach (var row in _source.Items)
                {
                    if (!row.IsManuallyEdited)
                        ApplyMatch(row, matcher.Match(row));
                }
            });
    }

    private Task Save()
    {
        var dir = Path.GetDirectoryName(CsvFilePath) ?? "";
        var nameWithoutExt = Path.GetFileNameWithoutExtension(CsvFilePath);
        var ext = Path.GetExtension(CsvFilePath);
        var outputPath = Path.Combine(dir, $"{nameWithoutExt}-categorized{ext}");

        var amountHeader = _amountNegated ? "amount_negated" : "amount";

        var csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            ShouldQuote = _ => true,
        };

        using var writer = new StreamWriter(outputPath);
        using var csv = new CsvWriter(writer, csvConfig);

        csv.WriteField("date");
        csv.WriteField("description");
        csv.WriteField(amountHeader);
        csv.WriteField("merchant");
        csv.WriteField("transfer_account");
        csv.NextRecord();

        foreach (var row in _source.Items.OrderBy(r => r.CsvIndex))
        {
            var description = string.IsNullOrEmpty(row.Merchant)
                ? row.Description
                : $"[{row.Merchant}] {row.Description}";

            csv.WriteField(row.Date.ToString("yyyy-MM-dd"));
            csv.WriteField(description);
            csv.WriteField(row.Amount);
            csv.WriteField(row.Merchant);
            csv.WriteField(row.SelectedAccount?.FullName ?? "");
            csv.NextRecord();
        }

        StatusMessage = $"Saved to {outputPath}";
        return Task.CompletedTask;
    }

    public async Task LoadCsv(string filePath)
    {
        CsvFilePath = filePath;

        var fileName = Path.GetFileName(filePath);
        var bankConfig = _configService.CurrentConfig.Banks.FirstOrDefault(bank => Regex.IsMatch(fileName, bank.Match));

        if (bankConfig is null)
        {
            await ShowError.Handle($"No bank configuration matched '{fileName}'. Check config.yml.");
            return;
        }

        _amountNegated = bankConfig.Headers.Split(',').Any(h => h.Trim() == "{-amount}");

        var rows = await _mediator!.Send(new ParseCsvRequest(filePath, bankConfig));
        var matcher = new MerchantMatcher(_configService.CurrentConfig.Merchants);

        _source.Edit(updater =>
        {
            updater.Clear();
            for (var i = 0; i < rows.Count; i++)
            {
                var rowVm = new CategorizationRowViewModel(rows[i].Date, rows[i].Description, rows[i].Amount, i);
                ApplyMatch(rowVm, matcher.Match(rowVm));
                updater.AddOrUpdate(rowVm);
            }
        });
    }

    private void ApplyMatch(CategorizationRowViewModel row, MatchResult? match)
    {
        if (match is null) return;
        
        row.SetFromConfig(
            match.Name,
            Accounts.FirstOrDefault(a => a.FullName == match.Account)
        );
    }

    public ViewModelActivator Activator { get; } = new();
}