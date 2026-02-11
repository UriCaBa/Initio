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

    private void LogCrash(Exception ex, string source)
    {
        string message = $"[{DateTime.Now}] Crash in {source}: {ex.Message}\n{ex.StackTrace}\n\nInner: {ex.InnerException?.Message}";
        try
        {
            System.IO.File.WriteAllText("crash_log.txt", message);
            MessageBox.Show($"Application Crashed!\n\n{ex.Message}\n\nSee crash_log.txt for details.", "Critical Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch { /* Panic */ }
    }
}
