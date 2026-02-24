using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using DynamicData;
using GnuCashUtils.Core;
using MediatR;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Splat;
using Unit = System.Reactive.Unit;


namespace GnuCashUtils.BulkEdit;

public partial class BulkEditScreenViewModel : ViewModelBase, IActivatableViewModel
{
    [Reactive] public partial Account? SourceAccount { get; set; }
    [Reactive] public partial Account? DestinationAccount { get; set; }
    [Reactive] public partial string SearchText { get; set; }
    private readonly Subject<Account> _refreshRequested = new();
    private readonly IScheduler _threadPoolScheduler;

    public ReactiveCommand<Unit, Unit> MoveCommand { get; }
    public ReactiveCommand<Unit, Unit> SelectAllCommand { get; }

    [ObservableAsProperty] public partial IReadOnlyCollection<Account> Accounts { get; }
    public ObservableCollection<SelectableTransactionViewModel> Transactions { get; } = new();

    public IObservable<int> TransactionCount { get; }
    public IObservable<int> FilteredTransactionCount { get; }
    public IObservable<int> SelectedTransactionCount { get; }

    public ViewModelActivator Activator { get; } = new();

    public BulkEditScreenViewModel(IMediator? mediator = null, IScheduler? threadPoolScheduler = null,
        IAccountStore? store = null)
    {
        _accounts = [];
        mediator ??= Locator.Current.GetRequiredService<IMediator>()!;
        store ??= Locator.Current.GetRequiredService<IAccountStore>();
        _threadPoolScheduler = threadPoolScheduler ?? TaskPoolScheduler.Default;
        SearchText = "";

        store.Accounts
            .Connect()
            .ObserveOn(RxApp.MainThreadScheduler)
            .ToSortedCollection(x => x.FullName)
            .ToProperty(this, x => x.Accounts, out _accountsHelper);

        this.WhenActivated(d =>
        {
            _accountsHelper.DisposeWith(d);
        });

        var accountTransactions = this.WhenAnyValue(x => x.SourceAccount)
            .Merge(_refreshRequested)
            .Where(x => x != null)
            .Select(x => x!.Guid)
            .Throttle(TimeSpan.FromMilliseconds(250), _threadPoolScheduler)
            .DistinctUntilChanged()
            .Select(accountGuid =>
                Observable.FromAsync((ct) => Task.Run(() => mediator.Send(new FetchTransactions(accountGuid), ct), ct)))
            .Switch()
            .Publish();
        accountTransactions.Connect();


        var searchText = this.WhenAnyValue(x => x.SearchText);

        accountTransactions
            .CombineLatest(searchText)
            .Select(x => x.Item1.Where(t => t.Description?.Contains(x.Item2, StringComparison.OrdinalIgnoreCase) == true))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(x =>
            {
                Transactions.Clear();
                Transactions.AddRange(x);
            });


        TransactionCount = accountTransactions.Select(t => t.Count);
        FilteredTransactionCount = this.WhenAnyValue(x => x.Transactions)
            .Select(t => t.Count);
        SelectedTransactionCount = this.WhenAnyValue(x => x.Transactions)
            .Select(t => t.Count(x => x.IsSelected));

        SelectAllCommand = ReactiveCommand.Create(
            execute: () =>
            {
                foreach (var t in Transactions)
                {
                    t.IsSelected = true;
                }
            },
            canExecute: TransactionCount.Select(n => n > 0));


        MoveCommand = ReactiveCommand.CreateFromTask(async () =>
            {
                if (SourceAccount == null || DestinationAccount == null) return;

                await mediator.Send(new MoveTransactionsCommand(Transactions, SourceAccount.Guid,
                    DestinationAccount.Guid));
                _refreshRequested.OnNext(SourceAccount);
            }
        );
    }
}

public partial class SelectableTransactionViewModel : ViewModelBase
{
    [Reactive] private string? _description;
    [Reactive] private decimal _amount;
    [Reactive] private DateTime _date;
    [Reactive] private bool _isSelected;
    [Reactive] private string _transactionGuid = "";
    [Reactive] private string? _splitGuid;
    [Reactive] private string? _accountGuid;
}
