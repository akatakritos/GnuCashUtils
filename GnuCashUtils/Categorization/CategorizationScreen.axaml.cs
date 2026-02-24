using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.ReactiveUI;
using Avalonia.VisualTree;
using GnuCashUtils.Core;
using ReactiveUI;

namespace GnuCashUtils.Categorization;

public partial class CategorizationScreen : ReactiveUserControl<CategorizationScreenViewModel>
{
    private CategorizationRowViewModel? _contextMenuTargetRow;

    public CategorizationScreen()
    {
        InitializeComponent();
        this.WhenActivated(d =>
        {
            d(ViewModel!.ShowError.RegisterHandler(async ctx =>
            {
                await ShowErrorDialog(ctx.Input);
                ctx.SetOutput(Unit.Default);
            }));

            d(ViewModel.WhenAnyValue(x => x.AccountTree)
                .WhereNotNull()
                .Subscribe(BuildContextMenu));
        });

        CsvDataGrid.AddHandler(PointerPressedEvent, OnDataGridPointerPressed, RoutingStrategies.Tunnel);
    }

    private void OnDataGridPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(CsvDataGrid).Properties.IsRightButtonPressed) return;

        _contextMenuTargetRow = (e.Source as Visual)
            ?.GetSelfAndVisualAncestors()
            .OfType<DataGridRow>()
            .FirstOrDefault()
            ?.DataContext as CategorizationRowViewModel;
    }

    private void BuildContextMenu(IReadOnlyCollection<Account> accountTree)
    {
        var contextMenu = new ContextMenu();
        foreach (var root in accountTree.OrderBy(a => a.Name))
            contextMenu.Items.Add(BuildMenuItem(root));
        CsvDataGrid.ContextMenu = contextMenu;
    }

    private MenuItem BuildMenuItem(Account account)
    {
        var item = new MenuItem { Header = account.Name };
        // Use PointerReleased (marked Handled) instead of Click so that intermediate nodes
        // (accounts with children) can be selected. Avalonia intercepts Click on parent MenuItems
        // to open the submenu, preventing Click from firing. PointerReleased bubbles from the
        // clicked node outward — marking it Handled stops ancestors from also firing their handlers.
        item.AddHandler(PointerReleasedEvent, (_, e) =>
        {
            ApplyAccount(account);
            CsvDataGrid.ContextMenu?.Close();
            e.Handled = true;
        }, RoutingStrategies.Bubble);
        foreach (var child in account.Children.OrderBy(c => c.Name))
            item.Items.Add(BuildMenuItem(child));
        return item;
    }

    private void ApplyAccount(Account account)
    {
        var selected = CsvDataGrid.SelectedItems.OfType<CategorizationRowViewModel>();
        foreach (var row in selected)
            row.SelectedAccount = account;
    }

    private async void OpenCsvButton_Click(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this)!;
        var result = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open CSV File",
            FileTypeFilter =
            [
                new FilePickerFileType("CSV Files") { Patterns = ["*.csv"] },
                new FilePickerFileType("All Files") { Patterns = ["*"] },
            ]
        });

        if (result.Count > 0)
        {
            await ViewModel!.LoadCsv(result[0].Path.AbsolutePath);
        }
    }

    private async Task ShowErrorDialog(string message)
    {
        var okButton = new Button { Content = "OK", HorizontalAlignment = HorizontalAlignment.Center };
        var dialog = new Window
        {
            Title = "Error",
            SizeToContent = SizeToContent.WidthAndHeight,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Content = new StackPanel
            {
                Margin = new Thickness(20),
                Spacing = 12,
                Children =
                {
                    new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, MaxWidth = 360 },
                    okButton
                }
            }
        };
        okButton.Click += (_, _) => dialog.Close();
        await dialog.ShowDialog((Window)TopLevel.GetTopLevel(this)!);
    }
}
