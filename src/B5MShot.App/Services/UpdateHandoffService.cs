using System.Diagnostics;
using System.Windows;
using System.Windows.Interop;
using B5MShot.Update;

namespace B5MShot.App.Services;

public sealed class UpdateHandoffService : IDisposable
{
    private readonly HwndSource _source;
    private readonly App _app;
    private readonly uint _message = UpdateHandoff.RegisterWindowMessage(UpdateHandoff.MessageName);

    public UpdateHandoffService(App app, Window mainWindow)
    {
        _app = app;
        _source = HwndSource.FromHwnd(new WindowInteropHelper(mainWindow).EnsureHandle())!;
        _source.AddHook(OnMessage);
    }

    private IntPtr OnMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if ((uint)message != _message) return IntPtr.Zero;
        handled = true;
        // Runs on the UI thread: reserve shutdown before another hotkey can start a capture.
        if (!_app.ReserveUpdateShutdown()) return new IntPtr(UpdateHandoff.Busy);
        _app.Dispatcher.BeginInvoke((Action)_app.ExitApplication);
        return new IntPtr(UpdateHandoff.Ready);
    }

    public static bool ReplaceOlderInstance(Mutex mutex)
    {
        var incoming = typeof(App).Assembly.GetName().Version!;
        foreach (var process in Process.GetProcessesByName("B5MShot"))
        {
            using (process)
            {
                if (process.Id == Environment.ProcessId) continue;
                var result = UpdateHandoff.Prepare(process.Id, incoming, false);
                if (result is "Busy" or "Unresponsive" or "Current") return false;
            }
        }
        try { return mutex.WaitOne(TimeSpan.FromSeconds(2)); }
        catch (AbandonedMutexException) { return true; }
    }

    public void Dispose() => _source.RemoveHook(OnMessage);
}
