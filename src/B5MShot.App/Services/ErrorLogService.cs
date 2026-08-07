using System.IO;
using System.Text;

namespace B5MShot.App.Services;

public static class ErrorLogService
{
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
