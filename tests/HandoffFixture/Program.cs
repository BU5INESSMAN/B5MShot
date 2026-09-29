using System;
using System.Windows;
using System.Windows.Interop;
using B5MShot.Update;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var main = new Window { Title = "B5MShot" };
        var handle = new WindowInteropHelper(main).EnsureHandle();
        foreach (var title in new[] { "Hidden Window", "SystemResourceNotifyWindow" })
            new WindowInteropHelper(new Window { Title = title }).EnsureHandle();
        // Match the hidden real tray main window, without touching settings/hotkeys/startup.
        if (Array.IndexOf(args, "editor") >= 0)
        {
            var editor = new Window { Title = "Unsaved screenshot", Width = 100, Height = 100 };
            new WindowInteropHelper(editor).EnsureHandle(); // Hidden editor must also block.
        }
        if (Array.IndexOf(args, "update-prompt") >= 0)
            app.Dispatcher.BeginInvoke((Action)(() => new Window { Title = UpdateHandoff.UpdatePromptTitle, Width = 160, Height = 80 }.ShowDialog()));
        if (Array.IndexOf(args, "protocol") >= 0 || Array.IndexOf(args, "busy") >= 0)
        {
            HwndSource.FromHwnd(handle).AddHook((IntPtr hwnd, int message, IntPtr w, IntPtr l, ref bool handled) =>
            {
                if ((uint)message != UpdateHandoff.RegisterWindowMessage(UpdateHandoff.MessageName)) return IntPtr.Zero;
                handled = true;
                if (Array.IndexOf(args, "busy") >= 0) return new IntPtr(UpdateHandoff.Busy);
                app.Dispatcher.BeginInvoke((Action)(() => app.Shutdown()));
                return new IntPtr(UpdateHandoff.Ready);
            });
        }
        app.Run();
    }
}
