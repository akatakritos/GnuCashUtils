using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
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
    private readonly IMediator? _mediator;
    private readonly IConfigService _configService;

    [Reactive] public partial string CsvFilePath { get; set; }
    public ObservableCollection<Account> Accounts { get; } = new();
    public ObservableCollection<CategorizationRowViewModel> Rows { get; } = new();
    public Interaction<string, Unit> ShowError { get; } = new();

    public CategorizationWindowViewModel(IMediator? mediator = null, IConfigService? configService = null)
    {
        CsvFilePath = "";
        _configService = configService ?? Locator.Current.GetService<IConfigService>() ?? new ConfigService();
        _mediator = mediator ?? Locator.Current.GetService<IMediator>();

        if (_mediator != null)
        {
            Observable.FromAsync(() => _mediator.Send(new FetchAccountsRequest()))
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
        var bankConfig = _configService.CurrentConfig.Banks.FirstOrDefault(bank => Regex.IsMatch(fileName, bank.Match));

        if (bankConfig is null)
        {
            await ShowError.Handle($"No bank configuration matched '{fileName}'. Check config.yml.");
            return;
        }

        var rows = await _mediator!.Send(new ParseCsvRequest(filePath, bankConfig));

        Rows.Clear();
        foreach (var row in rows)
            Rows.Add(new CategorizationRowViewModel(row.Date, row.Description, row.Amount, Accounts));
    }
}

