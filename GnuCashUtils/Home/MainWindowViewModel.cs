using System;
using System.Collections.Generic;
using System.IO;
using System.Reactive;
using Avalonia.Controls;
using GnuCashUtils.BulkEdit;
using GnuCashUtils.Categorization;
using GnuCashUtils.Core;
using GnuCashUtils.Reporting;
using GnuCashUtils.Tagger;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Splat;

namespace GnuCashUtils.Home;

public record NavItem(string Key, string Label, string Icon, Type ScreenVmType);

public partial class MainWindowViewModel : ViewModelBase
{
    public IReadOnlyList<NavItem> NavItems { get; } =
    [
        new("bulk-edit", "Bulk Edit", FontAwesomeIcons.PenToSquare, typeof(BulkEditScreenViewModel)),
        new("categorization", "Categorization", FontAwesomeIcons.FolderTree, typeof(CategorizationScreenViewModel)),
        new("tagger", "Tagger", FontAwesomeIcons.Tags, typeof(TaggerScreenViewModel)),
        new("reporting", "Reports", FontAwesomeIcons.ChartBar, typeof(ReportingScreenViewModel)),
    ];

    [Reactive] public partial NavItem? SelectedNavItem { get; set; }
    [Reactive] public partial string GnuCashFile { get; set; }
    [Reactive] public partial string CopyMessage { get; set; }

    public ReactiveCommand<Unit, Unit> BackupCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenFileCommand { get; }

    public MainWindowViewModel(IDbConnectionFactory? dbConnectionFactory = null, IConfigService? configService = null,
        IAccountStore? store = null)
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