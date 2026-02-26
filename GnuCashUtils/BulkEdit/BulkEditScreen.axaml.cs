using System.Reactive;
using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.ReactiveUI;
using ReactiveUI;

namespace GnuCashUtils.BulkEdit;

public partial class BulkEditScreen : ReactiveUserControl<BulkEditScreenViewModel>
{
     public ReactiveCommand<Unit, Unit> MoveCommand { get; }

    public BulkEditScreen()
    {
        InitializeComponent();

        var selectedItems = Observable
            .FromEventPattern<SelectionChangedEventArgs>(TransactionsGrid, nameof(DataGrid.SelectionChanged))
            .Select(_ => TransactionsGrid.SelectedItems)
            .Publish()
            .RefCount();

        var canMove = selectedItems.Select(i => i.Count > 0).StartWith(false);

        MoveCommand = ReactiveCommand.CreateFromObservable(
            () => ViewModel!.MoveCommand.Execute(),
            canMove);
        
        MoveButton.Command = MoveCommand;
    }
}