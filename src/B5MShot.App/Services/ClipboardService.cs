using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media.Imaging;

namespace B5MShot.App.Services;

public static class ClipboardService
{
    public static Task SetTextAsync(string text) => RetryAsync(() => System.Windows.Clipboard.SetText(text));
    public static Task SetImageAsync(BitmapSource image) => RetryAsync(() => System.Windows.Clipboard.SetImage(image));

    private static async Task RetryAsync(Action operation)
    {
        for (var attempt = 1; ; attempt++)
        {
            try { operation(); return; }
            catch (COMException) when (attempt < 6) { await Task.Delay(35 * attempt); }
        }
    }
    public static void SetText(string text) => Retry(() => System.Windows.Clipboard.SetText(text));

    public static void SetImage(BitmapSource image) => Retry(() => System.Windows.Clipboard.SetImage(image));

    private static void Retry(Action operation)
    {
        const int attempts = 6;
        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            try
            {
                operation();
                return;
            }
            catch (COMException) when (attempt < attempts)
            {
                Thread.Sleep(35 * attempt);
            }
        }
    }
}
