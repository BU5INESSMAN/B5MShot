using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Forms = System.Windows.Forms;
using Size = System.Windows.Size;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using B5MShot.App.Models;

namespace B5MShot.App.Services;

public static class ScreenPlacement
{
    public static Forms.Screen HudMonitor(HudPreferences preferences) =>
        Forms.Screen.AllScreens.FirstOrDefault(s => s.DeviceName == preferences.MonitorDeviceName)
        ?? Forms.Screen.FromPoint(Forms.Cursor.Position);

    public static Rect MonitorBounds(Window window, Forms.Screen screen)
    {
        var desktop = Forms.SystemInformation.VirtualScreen;
        var monitor = screen.Bounds;
        return new Rect((monitor.Left-desktop.Left)*window.ActualWidth/desktop.Width,
            (monitor.Top-desktop.Top)*window.ActualHeight/desktop.Height,
            monitor.Width*window.ActualWidth/desktop.Width, monitor.Height*window.ActualHeight/desktop.Height);
    }

    public static System.Windows.Point EdgeOrigin(Rect bounds, Size size, HudEdge edge) => edge switch
    {
        HudEdge.Bottom => new(bounds.Left+(bounds.Width-size.Width)/2, bounds.Bottom-size.Height),
        HudEdge.Left => new(bounds.Left, bounds.Top+(bounds.Height-size.Height)/2),
        HudEdge.Right => new(bounds.Right-size.Width, bounds.Top+(bounds.Height-size.Height)/2),
        _ => new(bounds.Left+(bounds.Width-size.Width)/2, bounds.Top)
    };

    public static CornerRadius EdgeCorners(HudEdge edge) => edge switch
    {
        HudEdge.Bottom => new(24,24,0,0), HudEdge.Left => new(0,24,24,0),
        HudEdge.Right => new(24,0,0,24), _ => new(0,0,24,24)
    };

    public static void PlaceHud(FrameworkElement hud, Rect bounds, Size size, HudEdge edge)
    {
        var point = EdgeOrigin(bounds, size, edge);
        hud.HorizontalAlignment = HorizontalAlignment.Left;
        hud.VerticalAlignment = VerticalAlignment.Top;
        hud.Margin = new Thickness(point.X, point.Y, 0, 0);
    }
    public static void PositionNearTray(Window window)
    {
        var work = Forms.Screen.FromPoint(Forms.Cursor.Position).WorkingArea;
        var dpi = System.Windows.Media.VisualTreeHelper.GetDpi(window);
        window.MaxHeight = Math.Max(160, work.Height / dpi.DpiScaleY - 16);
        window.Left = Math.Max(work.Left / dpi.DpiScaleX + 8, work.Right / dpi.DpiScaleX - window.ActualWidth - 8);
        window.Top = Math.Max(work.Top / dpi.DpiScaleY + 8, work.Bottom / dpi.DpiScaleY - window.ActualHeight - 8);
    }
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
