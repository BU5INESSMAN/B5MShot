using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Forms = System.Windows.Forms;
using Size = System.Windows.Size;
using HorizontalAlignment = System.Windows.HorizontalAlignment;

namespace B5MShot.App.Services;

public static class ScreenPlacement
{
    public static void FlushComposition() => DwmFlush();
    public static void CoverDesktop(Window window)
    {
        var bounds = Forms.SystemInformation.VirtualScreen;
        SetWindowPos(new WindowInteropHelper(window).Handle, new IntPtr(-1), bounds.Left, bounds.Top, bounds.Width, bounds.Height, 0x0040);
    }

    public static void PositionHud(FrameworkElement hud, Window window)
    {
        var desktop = Forms.SystemInformation.VirtualScreen;
        var monitor = Forms.Screen.FromPoint(Forms.Cursor.Position).Bounds;
        var scaleX = window.ActualWidth / desktop.Width;
        var scaleY = window.ActualHeight / desktop.Height;
        hud.HorizontalAlignment = HorizontalAlignment.Left;
        hud.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        hud.Margin = new Thickness(Math.Max(0, (monitor.Left - desktop.Left + monitor.Width / 2d) * scaleX - hud.DesiredSize.Width / 2),
            (monitor.Top - desktop.Top) * scaleY, 0, 0);
    }

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);

    [DllImport("dwmapi.dll")]
    private static extern int DwmFlush();
}
