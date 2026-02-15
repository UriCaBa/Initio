using System.Windows;

namespace NewPCSetupWPF;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Global exception handling
        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            LogCrash((Exception)args.ExceptionObject, "AppDomain");

        DispatcherUnhandledException += (s, args) =>
        {
            LogCrash(args.Exception, "Dispatcher");
            args.Handled = true; // Prevent immediate crash if possible
        };
    }

    private static void LogCrash(Exception ex, string source)
    {
        string message = $"[{DateTime.Now}] Crash in {source}: {ex.Message}\n{ex.StackTrace}\n\nInner: {ex.InnerException?.Message}";
        try
        {
            var crashDir = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Initio");
            System.IO.Directory.CreateDirectory(crashDir);
            var crashPath = System.IO.Path.Combine(crashDir, "crash_log.txt");
            System.IO.File.WriteAllText(crashPath, message);
            MessageBox.Show($"Application Crashed!\n\n{ex.Message}\n\nSee {crashPath} for details.", "Critical Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch { /* Panic */ }
    }
}
