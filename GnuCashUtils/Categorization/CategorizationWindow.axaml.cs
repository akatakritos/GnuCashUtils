using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.ReactiveUI;
using ReactiveUI;

namespace GnuCashUtils.Categorization;

public partial class CategorizationWindow : ReactiveWindow<CategorizationWindowViewModel>
{
    public CategorizationWindow()
    {
        InitializeComponent();
        this.WhenActivated(_ =>
        {
            this.WhenAnyValue(x => x.ViewModel!.Headers)
                .Subscribe(RebuildColumns);
        });
    }

    private async void OpenCsvButton_Click(object? sender, RoutedEventArgs e)
    {
        var result = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
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
            ViewModel!.LoadCsv(result[0].Path.AbsolutePath);
        }
    }

    private void RebuildColumns(IReadOnlyList<string>? headers)
    {
        CsvDataGrid.Columns.Clear();
        if (headers is null) return;

        for (int i = 0; i < headers.Count; i++)
        {
            CsvDataGrid.Columns.Add(new DataGridTextColumn
            {
                Header = headers[i],
                Binding = new Binding($"[{i}]"),
            });
        }
    }
}
