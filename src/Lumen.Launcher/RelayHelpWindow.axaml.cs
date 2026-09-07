using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace Lumen.Launcher;

public partial class RelayHelpWindow : Window
{
    public RelayHelpWindow()
    {
        InitializeComponent();
        var baseUri = "https://raw.githubusercontent.com/MartinCajiao/lumen-stream/main/scripts";
        OracleCommand.Text = $"irm {baseUri}/Deploy-OracleRelay.ps1 | iex";
        SimpleCommand.Text = $"irm {baseUri}/Deploy-SimpleRelay.ps1 | iex";
    }

    private void OnCopyOracle(object? sender, RoutedEventArgs e)
    {
        CopyToClipboard(OracleCommand.Text ?? "");
    }

    private void OnCopySimple(object? sender, RoutedEventArgs e)
    {
        CopyToClipboard(SimpleCommand.Text ?? "");
    }

    private void OnClose(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private async void CopyToClipboard(string text)
    {
        if (Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
        {
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                var top = TopLevel.GetTopLevel(this);
                if (top?.Clipboard is not null)
                {
                    top.Clipboard.SetTextAsync(text).GetAwaiter().GetResult();
                }
            });
        }
    }
}
