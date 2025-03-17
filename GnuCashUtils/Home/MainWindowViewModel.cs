using System;
using System.Reactive;
using Avalonia.ReactiveUI;
using GnuCashUtils.BulkEdit;
using GnuCashUtils.Core;
using ReactiveUI;
using Splat;

namespace GnuCashUtils.Home;

public class MainWindowViewModel : ViewModelBase
{
#pragma warning disable CA1822 // Mark members as static
    public string Greeting => "Welcome to Avalonia!";
#pragma warning restore CA1822 // Mark members as static
    
    public ReactiveCommand<Unit, Unit> BulkEditAccountCommand { get; }
    
    public MainWindowViewModel()
    {

        BulkEditAccountCommand = ReactiveCommand.Create(() =>
        {
            var viewLocator = Locator.Current.GetService<IViewLocator>();
            var viewModel = new BulkEditWindowViewModel(); // TODO: resolve this from container?
            var view = viewLocator!.ResolveView(viewModel);

            if (view is not ReactiveWindow<BulkEditWindowViewModel> window)
                throw new Exception("ViewModel does not have associated Window");

            window.ViewModel = viewModel;
            window.Show();
            return Unit.Default;
        });
    }
}