using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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
        ReconcileKeychain();

        UnhandledException += OnUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex) WriteCrashLog(ex, "AppDomain");
        };
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            WriteCrashLog(e.Exception, "TaskScheduler");
            // A faulted Task nobody awaited would tear down the process on GC;
            // we've logged it, so mark it observed and keep the app alive.
            e.SetObserved();
        };
    }

    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        WriteCrashLog(e.Exception, "WinUI");
        // X1 last-resort net: most handler exceptions are already caught by
        // Safe.Fire; anything that still reaches here (a framework callback, a
        // stray async-void lambda) would otherwise terminate the process. It's
        // logged — keep the app alive rather than losing the user's session over a
        // non-fatal glitch.
        e.Handled = true;
    }

    /// <summary>Logging entry point for exceptions caught by <see cref="Safe"/>.</summary>
    internal static void LogHandledException(Exception ex, string source) => WriteCrashLog(ex, source);

    // G2: prune vault tokens no stored account references. Skipped in demo mode
    // (no real vault) and a no-op when the accounts file can't be trusted. Wrapped
    // so vault I/O can never block or crash startup.
    private static void ReconcileKeychain()
    {
        if (IsDemoMode) return;
        try
        {
            if (Services.GetRequiredService<FlarePurge.Core.Auth.IAccountStore>() is FlarePurge.Core.Auth.JsonAccountStore store)
                FlarePurge.Core.Auth.KeychainReconciler.Reconcile(
                    store, Services.GetRequiredService<FlarePurge.Core.Auth.IKeychainProvider>());
        }
        catch { /* best-effort */ }
    }

    // Demo mode = Microsoft Store screenshot pipeline. Activated by any of:
    //   1. `-FPDemoMode 1` command-line arg (works when launched via the exe directly)
    //   2. `demo.flag` file in the package LocalFolder (works for packaged Shell launch)
    //   3. `FLAREPURGE_DEMO_MODE=1` environment variable (works for dev sessions)
    // Bypasses HTTP, cert pinning, DPAPI/PasswordVault and pre-seeds 2 fake CF
    // accounts with 8 demo zones so the UI can be captured without real creds.
    private static bool DetectDemoMode()
    {
#if !DEBUG
        // G3: demo mode swaps in fake in-memory data for the Store screenshot
        // pipeline. It must never be reachable in a Release/Store build — otherwise
        // a stray env var, launch arg or `demo.flag` file could flip the shipped app
        // into showing fabricated zones. The screenshot pipeline builds Debug.
        return false;
#else
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
#endif
    }

    // L1 (CWE-532): crash.log en claro sin sanitizar ni rotación. El mensaje de
    // una excepción puede arrastrar texto del servidor y zone/account IDs. Antes
    // de escribir: redactar secretos/identificadores y limitar el tamaño total.
    private const long CrashLogMaxBytes = 64 * 1024;
    private const int CrashLogMaxMessageChars = 500;

    // Bearer <token> → el token completo.
    [GeneratedRegex(@"(?i)\bBearer\s+[A-Za-z0-9._\-]+")]
    private static partial Regex BearerRegex();
    // Emails.
    [GeneratedRegex(@"[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}")]
    private static partial Regex EmailRegex();
    // Zone/account IDs de Cloudflare (32 hex exactos).
    [GeneratedRegex(@"\b[0-9a-fA-F]{32}\b")]
    private static partial Regex HexIdRegex();
    // Secuencias token-like largas (API tokens ~40 chars alnum/_/-).
    [GeneratedRegex(@"[A-Za-z0-9_\-]{30,}")]
    private static partial Regex LongTokenRegex();

    private static string Sanitize(string? input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        var s = input.Replace("\r", " ").Replace("\n", " ");
        s = BearerRegex().Replace(s, "Bearer [redacted]");
        s = EmailRegex().Replace(s, "[email]");
        s = HexIdRegex().Replace(s, "[id]");
        s = LongTokenRegex().Replace(s, "[token]");
        if (s.Length > CrashLogMaxMessageChars)
            s = s.Substring(0, CrashLogMaxMessageChars) + "…";
        return s;
    }

    private static void WriteCrashLog(Exception ex, string source)
    {
        try
        {
            var path = System.IO.Path.Combine(
                Windows.Storage.ApplicationData.Current.LocalFolder.Path,
                "crash.log");

            // Rotación: al superar el tope, mover el log actual a crash.log.1
            // (una generación anterior) y empezar fresco.
            try
            {
                var fi = new FileInfo(path);
                if (fi.Exists && fi.Length > CrashLogMaxBytes)
                {
                    var backup = path + ".1";
                    if (File.Exists(backup)) File.Delete(backup);
                    File.Move(path, backup);
                }
            }
            catch { }

            // El stack trace son nombres de tipo/método (sin datos de usuario),
            // pero lo sanitizamos igualmente por si un valor se coló en un frame.
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {source}: {ex.GetType().Name}: {Sanitize(ex.Message)}\n{Sanitize(ex.StackTrace)}\n\n";
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

    // C7: a second launch redirects its activation here instead of starting a new
    // process. Bring the existing window forward (it may be minimised to tray).
    internal void OnRedirected()
    {
        _window?.DispatcherQueue.TryEnqueue(() =>
        {
            if (_window is MainWindow main) main.BringToFront();
            else _window?.Activate();
        });
    }
}
