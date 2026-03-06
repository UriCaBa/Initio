using System.Text.RegularExpressions;
using System.Windows;
using Initio.Core.ViewModels;
using NewPCSetupWPF.Services;

namespace NewPCSetupWPF;

public partial class App : Application
{
    private static bool IsTestMode => string.Equals(Environment.GetEnvironmentVariable("INITIO_TEST_MODE"), "1", StringComparison.Ordinal);

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
        {
            var exception = args.ExceptionObject as Exception
                ?? new Exception(args.ExceptionObject?.ToString() ?? "Unknown unhandled exception");
            LogCrash(exception, "AppDomain");
        };

        DispatcherUnhandledException += (sender, args) =>
        {
            LogCrash(args.Exception, "Dispatcher");
            args.Handled = true;
        };

        MainViewModel viewModel = IsTestMode
            ? AppServiceFactory.CreateTestViewModel()
            : AppServiceFactory.CreateProductionViewModel();
        var window = new MainWindow(viewModel);
        MainWindow = window;
        window.Show();
    }

    private static void LogCrash(Exception ex, string source)
    {
        var message = BuildSanitizedCrashMessage(ex, source);
        try
        {
            var crashDirectory = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Initio");
            System.IO.Directory.CreateDirectory(crashDirectory);
            var crashPath = System.IO.Path.Combine(crashDirectory, "crash_log.txt");
            System.IO.File.WriteAllText(crashPath, message);
            MessageBox.Show(
                $"Application crashed and a sanitized diagnostic log was written to:{Environment.NewLine}{Environment.NewLine}{crashPath}",
                "Critical Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        catch
        {
        }
    }

    private static string BuildSanitizedCrashMessage(Exception ex, string source)
    {
        return string.Join(Environment.NewLine,
        [
            $"[{DateTime.Now:O}] Crash in {source}",
            $"Type: {ex.GetType().FullName}",
            $"Message: {Sanitize(ex.Message)}",
            $"StackTrace: {Sanitize(ex.StackTrace)}",
            $"Inner: {Sanitize(ex.InnerException?.Message)}"
        ]);
    }

    private static string Sanitize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "(none)";
        }

        var sanitized = Regex.Replace(value, @"[A-Za-z]:\\[^\r\n]+", "<path>");
        sanitized = Regex.Replace(sanitized, @"https?://\S+", "<url>");
        sanitized = Regex.Replace(sanitized, @"(?i)(api[_-]?key|token|secret|password)\s*[:=]\s*[^\s,;]+", "$1=<redacted>");
        return sanitized;
    }
}
