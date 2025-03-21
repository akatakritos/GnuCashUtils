using System;
using System.IO;
using System.Linq;
using System.Reactive;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.ReactiveUI;
using GnuCashUtils.BulkEdit;
using GnuCashUtils.Core;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Splat;

namespace GnuCashUtils.Home;

public partial class MainWindowViewModel : ViewModelBase
{
#pragma warning disable CA1822 // Mark members as static
    public string Greeting => "Welcome to Avalonia!";
#pragma warning restore CA1822 // Mark members as static
    
    public ReactiveCommand<Unit, Unit> BulkEditAccountCommand { get; }
    public ReactiveCommand<Unit, Unit> BackupCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenCommand { get; }
    [Reactive] public partial string GnuCashFile { get; set; }
    [Reactive] public partial string CopyMessage { get; set; }
    
    public MainWindowViewModel(IDbConnectionFactory? dbConnectionFactory = null)
    {
        dbConnectionFactory ??= Locator.Current.GetService<IDbConnectionFactory>();
        GnuCashFile = "/Users/mattburke/personal-copy.sqlite.gnucash";
        dbConnectionFactory.SetDatabase(GnuCashFile);


        if (Design.IsDesignMode)
        {
            CopyMessage = "Copied to /Users/mattburke/personal-copy.sqlite.12345.gnucash";
        }

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

        BackupCommand = ReactiveCommand.CreateRunInBackground(() =>
        {
            var ext = Path.GetExtension(GnuCashFile);
            var newPath = Path.ChangeExtension(GnuCashFile, DateTime.UtcNow.ToString("yyyyMMddHHmmss") + ext);
            File.Copy(GnuCashFile, newPath, overwrite: false);
            
            CopyMessage = "Copied to " + newPath;
        });
        
        this.WhenAnyValue(x => x.GnuCashFile)
            .Subscribe(file => dbConnectionFactory.SetDatabase(file));
        

    }
}