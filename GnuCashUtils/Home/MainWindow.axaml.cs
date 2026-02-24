using System;
using System.Collections.Generic;
using System.Reactive.Disposables;
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
    private readonly Dictionary<string, (ViewModelBase Vm, Control View)> _screenCache = new();

    public MainWindow()
    {
        InitializeComponent();
        this.WhenActivated((CompositeDisposable d) =>
        {
            this.WhenAnyValue(x => x.ViewModel!.SelectedNavItem)
                .Subscribe(ShowScreen)
                .DisposeWith(d);
        });
    }

    private void ShowScreen(NavItem? item)
    {
        if (item == null)
        {
            ContentArea.Content = null;
            return;
        }

        if (!_screenCache.TryGetValue(item.Key, out var cached))
        {
            var vm = (ViewModelBase)Locator.Current.GetService(item.ScreenVmType)!;
            var viewForType = typeof(IViewFor<>).MakeGenericType(item.ScreenVmType);
            var view = (Control)Locator.Current.GetService(viewForType)!;
            ((IViewFor)view).ViewModel = vm;
            cached = (vm, view);
            _screenCache[item.Key] = cached;
        }

        ContentArea.Content = cached.View;
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
