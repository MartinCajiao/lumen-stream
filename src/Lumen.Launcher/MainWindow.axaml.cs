using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace Lumen.Launcher;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm = new();
    private readonly DispatcherTimer _overlayTimer;
    private readonly DispatcherTimer _scanTimer;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _vm;
        _overlayTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(800) };
        _overlayTimer.Tick += (_, _) => _vm.TickOverlay();
        _overlayTimer.Start();
        _scanTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _scanTimer.Tick += async (_, _) =>
        {
            if (_vm.IsLoggedIn)
            {
                await _vm.ScanAsync();
            }
        };
        _scanTimer.Start();
        Closed += (_, _) =>
        {
            _overlayTimer.Stop();
            _scanTimer.Stop();
        };
    }

    private void OnSubmitAuth(object? sender, RoutedEventArgs e) => _vm.SubmitAuth();
    private void OnToggleAuth(object? sender, RoutedEventArgs e) => _vm.ToggleAuthMode();
    private void OnLogout(object? sender, RoutedEventArgs e) => _vm.Logout();
    private async void OnShare(object? sender, RoutedEventArgs e) => await _vm.ToggleShareAsync();
    private async void OnPair(object? sender, RoutedEventArgs e) => await _vm.PairAsync();
    private void OnAddRemote(object? sender, RoutedEventArgs e) => _vm.AddRemoteComputer();
    private async void OnConnectTyped(object? sender, RoutedEventArgs e) => await _vm.ConnectTypedAsync();

    private async void OnConnectCard(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: ComputerItem item })
        {
            await _vm.ConnectToAsync(item);
        }
    }
}
