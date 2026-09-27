using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Forms = System.Windows.Forms;

namespace B5MShot.App.Services;

public sealed class CaptureService
{
    public BitmapSource CaptureVirtualScreen()
    {
        var bounds = Forms.SystemInformation.VirtualScreen;
        using var bitmap = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppPArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bitmap.Size, CopyPixelOperation.SourceCopy);
        }

        var data = bitmap.LockBits(new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
        try
        {
            var source = BitmapSource.Create(bitmap.Width, bitmap.Height, 96, 96,
                System.Windows.Media.PixelFormats.Pbgra32, null, data.Scan0, data.Stride * data.Height, data.Stride);
            source.Freeze();
            return source;
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }

    public BitmapSource Crop(BitmapSource source, Rect selection, double viewWidth, double viewHeight)
    {
        var scaleX = source.PixelWidth / viewWidth;
        var scaleY = source.PixelHeight / viewHeight;
        var x = Math.Clamp((int)Math.Round(selection.X * scaleX), 0, source.PixelWidth - 1);
        var y = Math.Clamp((int)Math.Round(selection.Y * scaleY), 0, source.PixelHeight - 1);
        var width = Math.Clamp((int)Math.Round(selection.Width * scaleX), 1, source.PixelWidth - x);
        var height = Math.Clamp((int)Math.Round(selection.Height * scaleY), 1, source.PixelHeight - y);

        var cropped = new CroppedBitmap(source, new Int32Rect(x, y, width, height));
        cropped.Freeze();
        return cropped;
    }

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr hObject);
}

