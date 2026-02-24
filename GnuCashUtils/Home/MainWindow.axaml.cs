using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.ReactiveUI;
using GnuCashUtils.Core;
using ReactiveUI;
using Splat;

namespace GnuCashUtils.Home;

public partial class MainWindow : ReactiveWindow<MainWindowViewModel>
{
    private readonly Dictionary<Type, Control> _viewCache = new();

    public MainWindow()
    {
        InitializeComponent();
        this.WhenActivated(d =>
        {
            d(this.WhenAnyValue(x => x.ViewModel!.CurrentScreen)
                .Subscribe(ShowScreen));
        });
    }

    private void ShowScreen(ViewModelBase? vm)
    {
        if (vm == null)
        {
            ContentArea.Content = null;
            return;
        }

        var type = vm.GetType();
        if (!_viewCache.TryGetValue(type, out var view))
        {
            var viewForType = typeof(IViewFor<>).MakeGenericType(type);
            view = (Control)Locator.Current.GetService(viewForType)!;
            ((IViewFor)view).ViewModel = vm;
            _viewCache[type] = view;
        }

        ContentArea.Content = view;
    }

    private async void Button_OnClick(object? sender, RoutedEventArgs e)
    {
        var result = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions());
        if (result.Count > 0)
        {
            ViewModel!.GnuCashFile = result[0].Path.AbsolutePath;
        }
    }
}
