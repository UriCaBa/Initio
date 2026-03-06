using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using Initio.Core.Services;
using Initio.Core.ViewModels;

namespace NewPCSetupWPF;

public partial class MainWindow : Window
{
    private const string ThemePrefix = "/Themes/Theme.";
    private readonly MainViewModel _viewModel;
    private bool _isActivationRunning;

    public MainWindow(MainViewModel viewModel)
    {
        _viewModel = viewModel;
        InitializeComponent();
        DataContext = viewModel;
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        if (_viewModel.SelectedTheme is not null)
        {
            ApplyTheme(_viewModel.SelectedTheme.ResourcePath);
        }
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        _viewModel.UpdateLayoutMode(ActualWidth);
        await _viewModel.InitializeAsync();
    }

    private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        _viewModel.UpdateLayoutMode(e.NewSize.Width);
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.SelectedTheme) && _viewModel.SelectedTheme is not null)
        {
            ApplyTheme(_viewModel.SelectedTheme.ResourcePath);
        }
    }

    private void SidebarPane_ActivateWindowsRequested(object sender, RoutedEventArgs e)
    {
        ActivateWindows_Click(sender, e);
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        if (e.ClickCount == 2)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            return;
        }

        DragMove();
    }

    private void WindowMinimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void WindowMaxRestore_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void WindowClose_Click(object sender, RoutedEventArgs e) => Close();

    private void ActivateWindows_Click(object sender, RoutedEventArgs e)
    {
        if (_isActivationRunning)
        {
            _viewModel.AppendLog("Activation script is already running.");
            return;
        }

        var result = MessageBox.Show(
            "This will open an elevated PowerShell window and download Microsoft Activation Scripts (MAS) from the internet.\n\n" +
            "You will be prompted by UAC to grant administrator privileges.\n\nProceed?",
            "Activate Windows / Office",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            var powerShellPath = WindowsCommandPaths.TryGetPowerShellPath();
            if (string.IsNullOrWhiteSpace(powerShellPath))
            {
                _viewModel.AppendLog("Trusted PowerShell executable not found.");
                return;
            }

            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = powerShellPath,
                Arguments = "-NoProfile -ExecutionPolicy Bypass -Command \"irm https://get.activated.win | iex\"",
                Verb = "runas",
                UseShellExecute = true
            };
            var process = System.Diagnostics.Process.Start(startInfo);
            if (process is not null)
            {
                _isActivationRunning = true;
                _ = Task.Run(() =>
                {
                    process.WaitForExit();
                    _isActivationRunning = false;
                    Dispatcher.Invoke(() => _viewModel.AppendLog("Windows activation script closed."));
                });
            }

            _viewModel.AppendLog("Windows activation script launched (elevated PowerShell).");
        }
        catch (Win32Exception)
        {
            _viewModel.AppendLog("Windows activation cancelled (UAC declined).");
        }
        catch (Exception ex)
        {
            _viewModel.AppendLog($"Windows activation failed: {ex.Message}");
        }
    }

    private void ApplyTheme(string resourcePath)
    {
        var merged = Application.Current.Resources.MergedDictionaries;
        var current = merged.FirstOrDefault(dictionary =>
            dictionary.Source is not null &&
            dictionary.Source.OriginalString.StartsWith(ThemePrefix, StringComparison.OrdinalIgnoreCase));
        if (current is not null)
        {
            merged.Remove(current);
        }

        merged.Insert(0, new ResourceDictionary { Source = new Uri(resourcePath, UriKind.Relative) });
    }
}
