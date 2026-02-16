using System;
using System.Reactive.Linq;
using GnuCashUtils.BulkEdit;
using GnuCashUtils.Core;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace GnuCashUtils.Categorization;

public partial class CategorizationRowViewModel : ViewModelBase
{
    public DateOnly Date { get; init; }
    [Reactive] public partial string Description { get; set; }
    public decimal Amount { get; init; }
    [Reactive] public partial string Merchant { get; set; }
    [Reactive] public partial Account? SelectedAccount { get; set; }
    public bool IsManuallyEdited { get; private set; }
    
    private readonly ObservableAsPropertyHelper<bool> _isValid;
    public bool IsValid => _isValid.Value;
    
    private readonly ObservableAsPropertyHelper<string> _statusIcon;
    public string StatusIcon => _statusIcon.Value;
    
    private bool _applyingConfig;

    public int CsvIndex { get; }

    public CategorizationRowViewModel(DateOnly date, string description, decimal amount, int csvIndex = 0)
    {
        CsvIndex = csvIndex;
        Date = date;
        Description = description;
        Amount = amount;
        Merchant = "";

        _isValid = this.WhenAnyValue(x => x.SelectedAccount)
            .Select(a => a is not null)
            .ToProperty(this, x => x.IsValid);
        
        _statusIcon = this.WhenAnyValue(x => x.IsValid)
            .Select(valid => valid ? "✅" : "❌")
            .ToProperty(this, x => x.StatusIcon);

        this.WhenAnyValue(x => x.Description)
            .Skip(1)
            .Subscribe(_ => { IsManuallyEdited = true; });

        this.WhenAnyValue(x => x.Merchant)
            .Skip(1)
            .Subscribe(_ => {  IsManuallyEdited = true; });

        this.WhenAnyValue(x => x.SelectedAccount)
            .Skip(1)
            .Subscribe(_ => { IsManuallyEdited = true; });
    }

    public void SetFromConfig(string merchant, Account? account)
    {
        Merchant = merchant;
        SelectedAccount = account;
        IsManuallyEdited = false;
    }
}
