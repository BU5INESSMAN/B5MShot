namespace B5MShot.App.Models;

public sealed class AppSettings
{
    public string AreaCaptureHotKey { get; set; } = "PrintScreen";
    public string FullscreenCaptureHotKey { get; set; } = "Ctrl+PrintScreen";
    public bool StartWithWindows { get; set; }
}
