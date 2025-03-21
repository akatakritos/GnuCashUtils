using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.ReactiveUI;

namespace GnuCashUtils.Home;

public partial class MainWindow: ReactiveWindow<MainWindowViewModel>
{
    public MainWindow()
    {
        InitializeComponent();
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