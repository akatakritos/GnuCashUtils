using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text.RegularExpressions;
using System.Threading;
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
using Serilog;
using ILogger = Serilog.ILogger;

namespace GnuCashUtils.Categorization;

public partial class CategorizationWindowViewModel : ViewModelBase, IActivatableViewModel
{
    private static readonly ILogger _log = Log.ForContext<CategorizationWindowViewModel>();

    private readonly IMediator _mediator;
    private readonly IConfigService _configService;
    private readonly IClassifierBuilder _classifierBuilder;

    private BankConfig? _currentBankConfig;

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
    [ObservableAsProperty] public partial bool IsBuildingClassifier { get; }
    [ObservableAsProperty] public partial double BuildingProgress { get; }


    public CategorizationWindowViewModel(IMediator? mediator = null, IConfigService? configService = null,
        IAccountStore? store = null, IClassifierBuilder? classifierBuilder = null)
    {
        _accounts = [];
        _accountTree = [];

        CsvFilePath = "";
        StatusMessage = "";
        _configService = configService ?? Locator.Current.GetRequiredService<IConfigService>();
        _mediator = mediator ?? Locator.Current.GetRequiredService<IMediator>();
        _classifierBuilder = classifierBuilder ?? Locator.Current.GetRequiredService<IClassifierBuilder>()!;
        store ??= Locator.Current.GetRequiredService<IAccountStore>();

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
            _accountTreeHelper.DisposeWith(d);
        });


        var filter = this.WhenAnyValue(x => x.ShowOnlyErrors)
            .Select(showOnlyErrors =>
                showOnlyErrors ? new Func<CategorizationRowViewModel, bool>(row => !row.IsValid) : _ => true);

        _source.Connect()
            .Filter(filter)
            .Sort(Comparer<CategorizationRowViewModel>.Create((a, b) => a.CsvIndex.CompareTo(b.CsvIndex)))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Bind(out _filteredRows)
            .Subscribe();

        var errorCount = _source.Connect()
            .AutoRefresh(x => x.IsValid)
            .Filter(row => !row.IsValid)
            .Count()
            .Publish()
            .RefCount();

        errorCount.ToProperty(this, x => x.InvalidCount, out _invalidCountHelper);

        SaveCommand = ReactiveCommand.CreateFromTask(
            Save,
            canExecute: errorCount.Select(n => n == 0));


        _classifierBuilder.Status
            .Select(s => s == ClassifierBuilder.BuilderStatus.Running)
            .ToProperty(this, x => x.IsBuildingClassifier, out _isBuildingClassifierHelper);

        _classifierBuilder.Progress
            .Sample(Observable.Interval(TimeSpan.FromMilliseconds(100)))
            .ToProperty(this, x => x.BuildingProgress, out _buildingProgressHelper);
    }

    private Task Save()
    {
        Debug.Assert(_currentBankConfig is not null);
        
        var dir = Path.GetDirectoryName(CsvFilePath) ?? "";
        var nameWithoutExt = Path.GetFileNameWithoutExtension(CsvFilePath);
        var ext = Path.GetExtension(CsvFilePath);
        var outputPath = Path.Combine(dir, $"{nameWithoutExt}-categorized{ext}");

        var amountHeader = _currentBankConfig.Type == AccountType.Credit ? "amount_negated" : "amount";

        var csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            ShouldQuote = _ => true,
        };

        using var writer = new StreamWriter(outputPath);
        using var csv = new CsvWriter(writer, csvConfig);

        csv.WriteField("date");
        csv.WriteField("description");
        csv.WriteField(amountHeader);
        csv.WriteField("transfer_account");
        csv.NextRecord();

        foreach (var row in _source.Items.OrderBy(r => r.CsvIndex))
        {
            csv.WriteField(row.Date.ToString("yyyy-MM-dd"));
            csv.WriteField(row.Description);
            csv.WriteField(row.Amount);
            csv.WriteField(row.SelectedAccount?.FullName ?? "");
            csv.NextRecord();
        }

        _log.Information("Saved to {OutputPath}", outputPath);
        StatusMessage = $"Saved to {outputPath}";
        return Task.CompletedTask;
    }

    public async Task LoadCsv(string filePath, CancellationToken token = default)
    {
        var fileName = Path.GetFileName(filePath);
        var bankConfig = _configService.CurrentConfig.Banks.FirstOrDefault(bank => Regex.IsMatch(fileName, bank.Match));

        if (bankConfig is null)
        {
            await ShowError.Handle($"No bank configuration matched '{fileName}'. Check config.yml.");
            return;
        }

        var account = Accounts.FirstOrDefault(a => a.FullName == bankConfig.Account);
        if (account is null) throw new InvalidOperationException($"Account '{bankConfig.Account}' not found.");

        var classifier = await _classifierBuilder.Build(account.Guid, token);

        CsvFilePath = filePath;


        _currentBankConfig = bankConfig;

        var rows = await _mediator.Send(new ParseCsvRequest(filePath, bankConfig), token);

        _source.Edit(updater =>
        {
            updater.Clear();
            for (var i = 0; i < rows.Count; i++)
            {
                var rowVm = new CategorizationRowViewModel(rows[i].Date, rows[i].Description, rows[i].Amount, i);
                var prediction = classifier.Predict(rowVm.Description, rowVm.Amount);
                var predictedAccount = prediction.Confidence >= 0.5
                    ? Accounts.FirstOrDefault(a => a.FullName == prediction.Label)
                    : null;
                var ignoredLabel = prediction.Confidence < 0.5 ? prediction.Label : null;
                rowVm.SetFromClassifier(predictedAccount, prediction.Confidence, ignoredLabel);
                updater.AddOrUpdate(rowVm);
            }
        });
        _log.Information("Loaded {Count} rows from {FilePath}", rows.Count, filePath);
    }

    public ViewModelActivator Activator { get; } = new();

    #region Design Mode

    public CategorizationWindowViewModel()
    {
        _accounts = [];
        _accountTree = [];
        _mediator = null!;
        _configService = null!;
        _classifierBuilder = null!;

        CsvFilePath = "/Users/demo/Downloads/Discover-2026-01.csv";
        StatusMessage = "";

        _source.Connect()
            .Sort(Comparer<CategorizationRowViewModel>.Create((a, b) => a.CsvIndex.CompareTo(b.CsvIndex)))
            .Bind(out _filteredRows)
            .Subscribe();

        var sampleAccounts = new List<Account>
        {
            new()
            {
                Guid = "g1", Name = "Groceries", FullName = "Expenses:Food:Groceries", ParentGuid = "",
                Children = []
            },
            new()
            {
                Guid = "g2", Name = "Dining Out", FullName = "Expenses:Food:Dining Out", ParentGuid = "",
                Children = []
            },
            new() { Guid = "g3", Name = "Gas", FullName = "Expenses:Auto:Gas", ParentGuid = "", Children = [] },
        };

        Observable.Return((IReadOnlyCollection<Account>)sampleAccounts)
            .ToProperty(this, x => x.Accounts, out _accountsHelper);
        Observable.Return((IReadOnlyCollection<Account>)[])
            .ToProperty(this, x => x.AccountTree, out _accountTreeHelper);
        Observable.Return(false)
            .ToProperty(this, x => x.IsBuildingClassifier, out _isBuildingClassifierHelper);
        Observable.Return(0.0)
            .ToProperty(this, x => x.BuildingProgress, out _buildingProgressHelper);

        SaveCommand = ReactiveCommand.Create(() => { });

        _source.Edit(updater =>
        {
            (DateOnly Date, string Desc, decimal Amount, Account? Account, double Conf, string? Ignored)[] rows =
            [
                (new(2026, 1, 15), "WHOLE FOODS MARKET KC MO", -87.43m, sampleAccounts[0], 0.91, null),
                (new(2026, 1, 14), "NETFLIX.COM 866-579-7172 CA", -15.99m, sampleAccounts[1], 0.83, null),
                (new(2026, 1, 13), "SHELL OIL 12345 LEES SUMMIT MO", -52.11m, sampleAccounts[2], 0.61, null),
                (new(2026, 1, 12), "HOBBY-LOBBY #697 BELTON MO", -34.00m, null, 0.31, "Expenses:Shopping:Hobbies"),
                (new(2026, 1, 11), "GRAPES AND GRAINS KANSAS CITY MO", -99.38m, null, 0.22,
                    "Expenses:Food:Dining Out"),
                (new(2026, 1, 10), "LONGHORN STEAK 012345 INDEPENDENCE", -63.51m, null, 0.18,
                    "Expenses:Food:Dining Out"),
                (new(2026, 1, 15), "ADP PAYCHECK 123", -1752.51m, null, 0.87,
                    "Income:Salary"),
            ];

            for (var i = 0; i < rows.Length; i++)
            {
                var (date, desc, amount, account, conf, ignored) = rows[i];
                var vm = new CategorizationRowViewModel(date, desc, amount, i);
                vm.SetFromClassifier(account, conf, ignored);
                updater.AddOrUpdate(vm);
            }
        });

        Observable.Return(_source.Items.Count(r => !r.IsValid))
            .ToProperty(this, x => x.InvalidCount, out _invalidCountHelper);
    }

    #endregion
}