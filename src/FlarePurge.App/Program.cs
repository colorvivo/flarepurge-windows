using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;

namespace FlarePurge.App;

// C7: custom entry point (replaces the XAML-generated Main via
// DISABLE_XAML_GENERATED_MAIN) that enforces a single running instance. A second
// launch redirects its activation to the first instance — which brings its window
// forward — and exits, instead of running a second process that races the first on
// the shared JSON state files.
public static class Program
{
    private const uint CWMO_DEFAULT = 0;
    private const uint INFINITE = 0xFFFFFFFF;

    [STAThread]
    private static int Main(string[] args)
    {
        WinRT.ComWrappersSupport.InitializeComWrappers();

        if (DecideRedirection())
            return 0; // a first instance already owns the key — we redirected and exit.

        Application.Start(p =>
        {
            var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            _ = new App();
        });
        return 0;
    }

    private static bool DecideRedirection()
    {
        var activationArgs = AppInstance.GetCurrent().GetActivatedEventArgs();
        var keyInstance = AppInstance.FindOrRegisterForKey("ColorvivoInternet.FlarePurge");

        if (keyInstance.IsCurrent)
        {
            keyInstance.Activated += OnActivated;
            return false;
        }

        RedirectActivationTo(activationArgs, keyInstance);
        return true;
    }

    private static void OnActivated(object? sender, AppActivationArguments args)
        => (Application.Current as App)?.OnRedirected();

    private static IntPtr _redirectEventHandle = IntPtr.Zero;

    // The redirect must complete before this process exits, and COM must keep
    // pumping while we wait (per Microsoft's single-instancing sample) — hence the
    // event + CoWaitForMultipleObjects rather than a bare .Wait().
    private static void RedirectActivationTo(AppActivationArguments args, AppInstance keyInstance)
    {
        _redirectEventHandle = CreateEvent(IntPtr.Zero, true, false, null);
        Task.Run(() =>
        {
            keyInstance.RedirectActivationToAsync(args).AsTask().Wait();
            SetEvent(_redirectEventHandle);
        });
        _ = CoWaitForMultipleObjects(
            CWMO_DEFAULT, INFINITE, 1, new[] { _redirectEventHandle }, out _);
    }

    [DllImport("ole32.dll")]
    private static extern uint CoWaitForMultipleObjects(
        uint dwFlags, uint dwMilliseconds, ulong nHandles, IntPtr[] pHandles, out uint dwIndex);

    [DllImport("kernel32.dll")]
    private static extern IntPtr CreateEvent(
        IntPtr lpEventAttributes, bool bManualReset, bool bInitialState, string? lpName);

    [DllImport("kernel32.dll")]
    private static extern bool SetEvent(IntPtr hEvent);
}
