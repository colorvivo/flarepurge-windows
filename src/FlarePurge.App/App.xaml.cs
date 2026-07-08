using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FlarePurge.App.Localization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;

namespace FlarePurge.App;

public partial class App : Application
{
    private Window? _window;
    private TrayIconController? _trayController;

    public static IServiceProvider Services { get; private set; } = default!;
    public static bool IsDemoMode { get; private set; }

    public App()
    {
        InitializeComponent();
        IsDemoMode = DetectDemoMode();
        Services = DependencyInjection.Build(IsDemoMode);
        LocalizationBootstrap.Install(Services.GetRequiredService<FlarePurge.Core.Auth.IAccountStore>());

        UnhandledException += OnUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex) WriteCrashLog(ex, "AppDomain");
        };
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            WriteCrashLog(e.Exception, "TaskScheduler");
        };
    }

    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        WriteCrashLog(e.Exception, "WinUI");
    }

    // Demo mode = Microsoft Store screenshot pipeline. Activated by any of:
    //   1. `-FPDemoMode 1` command-line arg (works when launched via the exe directly)
    //   2. `demo.flag` file in the package LocalFolder (works for packaged Shell launch)
    //   3. `FLAREPURGE_DEMO_MODE=1` environment variable (works for dev sessions)
    // Bypasses HTTP, cert pinning, DPAPI/PasswordVault and pre-seeds 2 fake CF
    // accounts with 8 demo zones so the UI can be captured without real creds.
    private static bool DetectDemoMode()
    {
        try
        {
            var args = Environment.GetCommandLineArgs();
            if (args.Any(a => string.Equals(a, "-FPDemoMode", StringComparison.OrdinalIgnoreCase)
                || string.Equals(a, "--demo", StringComparison.OrdinalIgnoreCase)))
                return true;
        }
        catch { }

        try
        {
            if (string.Equals(Environment.GetEnvironmentVariable("FLAREPURGE_DEMO_MODE"), "1", StringComparison.Ordinal))
                return true;
        }
        catch { }

        try
        {
            var flag = Path.Combine(
                Windows.Storage.ApplicationData.Current.LocalFolder.Path,
                "demo.flag");
            if (File.Exists(flag)) return true;
        }
        catch { }

        return false;
    }

    private static void WriteCrashLog(Exception ex, string source)
    {
        try
        {
            var path = System.IO.Path.Combine(
                Windows.Storage.ApplicationData.Current.LocalFolder.Path,
                "crash.log");
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {source}: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}\n\n";
            System.IO.File.AppendAllText(path, line);
        }
        catch { }
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        _trayController = new TrayIconController(
            _window,
            Services.GetRequiredService<FlarePurge.Core.Auth.IAccountStore>(),
            Services.GetRequiredService<FlarePurge.Core.Services.ICacheService>());
        _trayController.Initialize();
        _window.Closed += (_, _) => _trayController?.Dispose();
        _window.Activate();
    }
}
