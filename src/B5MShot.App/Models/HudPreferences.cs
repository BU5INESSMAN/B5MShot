namespace B5MShot.App.Models;

public enum HudEdge { Top, Bottom, Left, Right }

public sealed record HudPreferences(HudEdge Edge = HudEdge.Top, string MonitorDeviceName = "")
{
    public bool IsVertical => Edge is HudEdge.Left or HudEdge.Right;
    public static HudPreferences FromSettings(AppSettings settings) => new(
        Enum.TryParse<HudEdge>(settings.ToolbarEdge, out var edge) && Enum.IsDefined(edge) ? edge : HudEdge.Top,
        settings.ToolbarMonitor ?? "");
}
