using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Avalonia.ReactiveUI;

namespace GnuCashUtils.BulkEdit;

public partial class BulkEditWindow : ReactiveWindow<BulkEditWindowViewModel>
{
    public BulkEditWindow()
    {
        InitializeComponent();
    }

}