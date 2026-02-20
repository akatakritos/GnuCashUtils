using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;

namespace GnuCashUtils.Tagger;

public partial class TaggerWindow : ReactiveWindow<TaggerWindowViewModel>
{
    public TaggerWindow()
    {
        InitializeComponent();
    }
}