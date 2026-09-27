using System.IO;
using System.Text;

namespace B5MShot.App.Services;

public static class ErrorLogService
{
    public static void CaptureTiming(long elapsedMilliseconds, int width, int height)
    {
        // Only timing and dimensions; never pixels, clipboard contents or window titles.
        _ = Task.Run(() =>
        {
            try
            {
                lock (SyncRoot)
                {
                    var directory = Path.GetDirectoryName(LogPath)!;
                    Directory.CreateDirectory(directory);
                    var path = Path.Combine(directory, "capture-timing.log");
                    if (File.Exists(path) && new FileInfo(path).Length > 64 * 1024) File.WriteAllText(path, string.Empty);
                    File.AppendAllText(path, $"{DateTimeOffset.Now:O} ready_ms={elapsedMilliseconds} pixels={width}x{height}{Environment.NewLine}");
                }
            }
            catch { /* Diagnostics must not affect capturing. */ }
        });
    }
    private static readonly object SyncRoot = new();

    public static string LogPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "B5MShot",
        "logs",
        "errors.log");

    public static void Write(Exception exception, string context)
    {
        try
        {
            lock (SyncRoot)
            {
                var directory = Path.GetDirectoryName(LogPath)!;
                Directory.CreateDirectory(directory);
                var entry = new StringBuilder()
                    .AppendLine($"[{DateTimeOffset.Now:O}] {context}")
                    .AppendLine(exception.ToString())
                    .AppendLine(new string('-', 72))
                    .ToString();
                File.AppendAllText(LogPath, entry);

                const long maximumLogSize = 2 * 1024 * 1024;
                var file = new FileInfo(LogPath);
                if (file.Length > maximumLogSize)
                {
                    var text = File.ReadAllText(LogPath);
                    var charactersToKeep = Math.Min(text.Length, (int)(maximumLogSize / 2));
                    File.WriteAllText(LogPath, text[^charactersToKeep..]);
                }
            }
        }
        catch
        {
            // Logging must never become another application failure.
        }
    }
}
