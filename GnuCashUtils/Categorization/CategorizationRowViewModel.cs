using System;
using System.Collections.ObjectModel;
using GnuCashUtils.BulkEdit;
using GnuCashUtils.Core;
using ReactiveUI.SourceGenerators;

namespace GnuCashUtils.Categorization;

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