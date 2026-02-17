using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DynamicData;
using GnuCashUtils.BulkEdit;
using GnuCashUtils.Core;
using MediatR;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Splat;
using Unit = System.Reactive.Unit;
using DynamicData.Aggregation;

namespace GnuCashUtils.Categorization;

public partial class CategorizationWindowViewModel : ViewModelBase
{
    private readonly IMediator? _mediator;
    private readonly IConfigService _configService;

    [Reactive] public partial string CsvFilePath { get; set; }
    [Reactive] public partial bool ShowOnlyErrors { get; set; }
    public ObservableCollection<Account> Accounts { get; } = new();
    public Interaction<string, Unit> ShowError { get; } = new();

    private readonly SourceCache<CategorizationRowViewModel, int> _source = new(row => row.CsvIndex);
    private readonly ReadOnlyObservableCollection<CategorizationRowViewModel> _filteredRows;
    public ReadOnlyObservableCollection<CategorizationRowViewModel> Rows => _filteredRows;

    [ObservableAsProperty] public partial int InvalidCount { get; }
    public ReactiveCommand<Unit, Unit> SaveCommand { get; }

    public CategorizationWindowViewModel(IMediator? mediator = null, IConfigService? configService = null)
    {
        CsvFilePath = "";
        _configService = configService ?? Locator.Current.GetService<IConfigService>() ?? new ConfigService();
        _mediator = mediator ?? Locator.Current.GetService<IMediator>()!;

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
            () => Task.FromResult(Unit.Default),
            canExecute: errorCount.Select(n => n == 0));

        Observable.FromAsync(() => _mediator.Send(new FetchAccountsRequest()))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(accounts =>
            {
                Accounts.AddRange(accounts);
            });

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
}