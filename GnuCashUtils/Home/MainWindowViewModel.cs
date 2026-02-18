using System;
using System.IO;
using System.Linq;
using System.Reactive;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.ReactiveUI;
using GnuCashUtils.BulkEdit;
using GnuCashUtils.Categorization;
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
    public ReactiveCommand<Unit, Unit> CategorizationCommand { get; }
    public ReactiveCommand<Unit, Unit> BackupCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenFileCommand { get; }
    [Reactive] public partial string GnuCashFile { get; set; }
    [Reactive] public partial string CopyMessage { get; set; }
    
    public MainWindowViewModel(IDbConnectionFactory? dbConnectionFactory = null, IConfigService? configService = null, IAccountStore? store = null)
    {
        dbConnectionFactory ??= Locator.Current.GetService<IDbConnectionFactory>()!;
        configService ??= Locator.Current.GetService<IConfigService>()!;
        store ??= Locator.Current.GetService<IAccountStore>()!;
        GnuCashFile = configService!.CurrentConfig.Database;
        dbConnectionFactory.SetDatabase(GnuCashFile);
        _copyMessage = "";

        OpenFileCommand = ReactiveCommand.CreateFromTask(() => store.Load());
        OpenFileCommand.Execute();


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

        CategorizationCommand = ReactiveCommand.Create(() =>
        {
            var viewLocator = Locator.Current.GetRequiredService<IViewLocator>();
            var viewModel = Locator.Current.GetRequiredService<CategorizationWindowViewModel>();
            var view = viewLocator.ResolveView(viewModel);

            if (view is not ReactiveWindow<CategorizationWindowViewModel> window)
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