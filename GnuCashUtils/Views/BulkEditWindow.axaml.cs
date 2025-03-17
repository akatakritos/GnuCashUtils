using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using GnuCashUtils.ViewModels;

namespace GnuCashUtils.Views;

public partial class BulkEditWindow : ReactiveWindow<BulkEditWindowViewModel>
{
    public BulkEditWindow()
    {
        InitializeComponent();
    }
}