using System.Reactive;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.ReactiveUI;
using ReactiveUI;

namespace GnuCashUtils.Categorization;

public partial class CategorizationWindow : ReactiveWindow<CategorizationWindowViewModel> 
{
    public CategorizationWindow()
    {
        InitializeComponent();
        this.WhenActivated(d =>
        {
            d(ViewModel!.ShowError.RegisterHandler(async ctx =>
            {
                await ShowErrorDialog(ctx.Input);
                ctx.SetOutput(Unit.Default);
            }));
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
        await dialog.ShowDialog(this);
    }
}
