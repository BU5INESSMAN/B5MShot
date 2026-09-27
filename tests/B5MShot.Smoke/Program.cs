using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using B5MShot.App.Services;
using B5MShot.App.Views;
using B5MShot.App.Controls;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        var app = new Application();
        app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/B5MShot;component/Themes/Glass.xaml") });
        app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        try
        {
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.DrawRectangle(Brushes.CornflowerBlue, null, new Rect(0, 0, 960, 540));
                dc.DrawRectangle(Brushes.WhiteSmoke, null, new Rect(120, 100, 720, 340));
            }
            var fixture = new RenderTargetBitmap(960, 540, 96, 96, PixelFormats.Pbgra32);
            fixture.Render(visual); fixture.Freeze();
            var capture = new CaptureService();
            if (args.Contains("--selection"))
            {
                // A synthetic desktop must share the real virtual desktop's aspect ratio.
                var width = (int)SystemParameters.VirtualScreenWidth;
                var height = (int)SystemParameters.VirtualScreenHeight;
                using (var dc = visual.RenderOpen())
                {
                    dc.DrawRectangle(Brushes.CornflowerBlue, null, new Rect(0, 0, width, height));
                    dc.DrawRectangle(Brushes.WhiteSmoke, null, new Rect(width * .2, 120, width * .6, height - 240));
                }
                fixture = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
                fixture.Render(visual); fixture.Freeze();
                Console.WriteLine("Opening synthetic selection window");
                var selection = new SelectionWindow(fixture, capture);
                // Expose the otherwise taskbar-less overlay to desktop UI test tools.
                selection.ShowInTaskbar = true;
                Console.WriteLine("Selection window constructed");
                var editing = false;
                selection.CaptureFinished += (_, image) =>
                {
                    editing = true;
                    var editor = new PreviewWindow(image, new UploadService(), fixture, selection.SelectedRegion);
                    editor.ShowInTaskbar = true;
                    editor.Closed += (_, _) => Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Background);
                    editor.Show();
                };
                selection.Closed += (_, _) => { if (!editing) Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Background); };
                selection.Show(); Console.WriteLine("Selection window shown"); Dispatcher.Run(); return 0;
            }
            var crop = capture.Crop(fixture, new Rect(60, 50, 360, 170), 480, 270);
            Check(crop.PixelWidth == 720 && crop.PixelHeight == 340, "DPI-scaled crop preserves pixel dimensions");
            var corner = capture.Crop(fixture, new Rect(479, 269, 20, 20), 480, 270);
            Check(corner.PixelWidth == 2 && corner.PixelHeight == 2, "Crop clamps at screen edge");
            var window = new PreviewWindow(fixture, new UploadService());
            window.Show(); Pump(650);
            Console.WriteLine($"Windows animations enabled: {Motion.Enabled}");
            if (args.Contains("--interactive"))
            {
                window.Closed += (_, _) => Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Background);
                Dispatcher.Run(); return 0;
            }
            var initial = (BitmapSource)Call(window, "RenderFinalImage")!;
            Check(PixelsEqual(fixture, initial), "Export contains original image only, no HUD");
            Call(window, "SetPaletteOpen", true); Pump(90);
            var clip = (EdgeSurface)window.FindName("PaletteClip");
            var bar = (Border)window.FindName("HudBar");
            if (Motion.Enabled) Check(clip.Reveal > .01 && clip.Reveal < .99, "Palette has visible intermediate animation frames");
            var barHeight = bar.ActualHeight;
            Pump(470);
            Check(clip.ActualWidth < bar.ActualWidth && clip.ActualHeight > 250, "Palette opens narrow and fully visible");
            Check(Math.Abs(bar.ActualHeight - barHeight) < .1 && clip.Reveal == 1, $"Toolbar height stays fixed while palette expands ({barHeight} -> {bar.ActualHeight}, reveal={clip.Reveal})");
            Check(PixelsEqual(fixture, (BitmapSource)Call(window, "RenderFinalImage")!), "Open palette never enters export");
            var hex = (TextBox)window.FindName("HexText");
            hex.Text = "#12AB34"; Call(window, "HexCommitted", hex, new RoutedEventArgs()); Pump(160);
            var dot = (System.Windows.Shapes.Ellipse)window.FindName("ColorDot");
            var visibleColor = ((SolidColorBrush)dot.Fill).Color;
            Check(visibleColor.R == 18 && visibleColor.G == 171 && visibleColor.B == 52, $"HEX and visible color match ({visibleColor})");
            var slider = (Slider)window.FindName("StrokeSlider"); slider.Value = 20; Pump(220);
            Check(Math.Abs(dot.ActualWidth - 26) < .1, "One button displays thickness through diameter");
            var tool = ((StackPanel)window.FindName("ToolPanel")).Children.OfType<ToggleButton>().Single(b => (string)b.Tag == "Arrow");
            tool.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            var toolIcon = (ContentPresenter)tool.Template.FindName("MotionContent", tool);
            var iconMotion = (TransformGroup)toolIcon.RenderTransform;
            double peakRecoil=0;
            for(var sample=0;sample<8;sample++){Pump(25);peakRecoil=Math.Max(peakRecoil,Math.Abs(((RotateTransform)iconMotion.Children[1]).Angle));}
            if (Motion.Enabled) Check(peakRecoil > .1, "Icon visibly recoils on activation");
            Pump(390);
            Check(Math.Abs(((RotateTransform)iconMotion.Children[1]).Angle) < .01, "Icon settles instead of looping");
            Check(tool.IsChecked == true, "Tool switch updates selection");
            Check(Math.Abs(((TranslateTransform)window.FindName("ToolIndicator")).X - 90) < .1, "Spring indicator settles at the chosen tool");
            Call(window, "SetPaletteOpen", false); Pump(50);
            var interrupted = clip.Reveal;
            Call(window, "SetPaletteOpen", true);
            Check(Math.Abs(clip.Reveal - interrupted) < .08, "Rapid reversal starts from current position");
            Pump(520);
            Check(clip.Reveal == 1 && clip.IsHitTestVisible, "Interrupted palette finishes in the newest requested state");
            Call(window, "SetPaletteOpen", false); Pump(280);
            Check(clip.Reveal == 0 && !clip.IsHitTestVisible && !((StackPanel)window.FindName("PaletteContent")).IsEnabled, "Closed palette cannot intercept input");
            var frame = (SelectionOutline)window.FindName("SelectionFrame");
            Check(frame.ActualWidth > 0 && !frame.IsHitTestVisible, "Imported image has a non-intercepting selection outline");
            window.Close(); Pump(260);
            Check(!window.IsVisible, "Editor exits after its short closing animation");
            var inline = new PreviewWindow(crop, new UploadService(), fixture, new Rect(.125, .185185, .75, .62963));
            inline.Show(); Pump(650);
            Check(inline.WindowStyle == WindowStyle.None && !inline.ShowInTaskbar, "Inline editor uses desktop overlay");
            Check(PixelsEqual(crop, (BitmapSource)Call(inline, "RenderFinalImage")!), "Inline export excludes selection outline, desktop and HUD");
            var inlineFrame = (SelectionOutline)inline.FindName("SelectionFrame");
            Check(Math.Abs(inlineFrame.ActualWidth - inline.ActualWidth * .75) < 1, "Inline outline follows selected desktop region");
            inline.Close(); Pump(260);
            AppContext.SetSwitch("B5MShot.DisableAnimations", true);
            var reduced = new PreviewWindow(fixture, new UploadService()); reduced.Show(); Pump(100);
            Call(reduced, "SetPaletteOpen", true);
            Check(((EdgeSurface)reduced.FindName("PaletteClip")).Reveal == 1, "Reduced motion opens instantly in a readable state");
            Call(reduced, "SetPaletteOpen", false);
            Check(((EdgeSurface)reduced.FindName("PaletteClip")).Reveal == 0, "Reduced motion closes instantly");
            reduced.Close(); Check(!reduced.IsVisible, "Reduced motion has no closing delay");
            var settings = new B5MShot.App.MainWindow(new SettingsService(), new AutoStartService());
            settings.Show(); Pump(100);
            var shell = (FrameworkElement)settings.FindName("Shell");
            settings.HideAnimatedAsync().GetAwaiter().GetResult();
            settings.Show(); Pump(100);
            Check(shell.Opacity == 1, "Settings reopens visibly with reduced motion");
            Check(((TextBlock)settings.FindName("VersionText")).Text.Contains("0.8.0"), "Settings displays release version");
            settings.Close();
            var tray = new TrayMenuWindow("Print Screen", _ => { }); tray.Show(); Pump(100);
            Check(!tray.ShowInTaskbar && tray.ActualWidth == 316, "Tray menu stays compact and off taskbar");
            Check(((FrameworkElement)tray.FindName("Surface")).Opacity == 1, "Tray menu supports reduced motion");
            tray.Close();
            Check(B5MShot.App.Models.HudPreferences.FromSettings(new(){ToolbarEdge="invalid"}).Edge == B5MShot.App.Models.HudEdge.Top, "Unknown edge safely defaults to top");
            var releaseJson="""{"tag_name":"v0.8.0","html_url":"https://github.com/BU5INESSMAN/B5MShot/releases/tag/v0.8.0","body":"Release","draft":false,"prerelease":false,"assets":[{"name":"B5MShot.exe","browser_download_url":"https://github.com/BU5INESSMAN/B5MShot/releases/download/v0.8.0/B5MShot.exe"},{"name":"B5MShot-Setup.exe","browser_download_url":"https://github.com/BU5INESSMAN/B5MShot/releases/download/v0.8.0/B5MShot-Setup.exe"},{"name":"SHA256SUMS.txt","browser_download_url":"https://github.com/BU5INESSMAN/B5MShot/releases/download/v0.8.0/SHA256SUMS.txt"}]}""";
            using(var document=System.Text.Json.JsonDocument.Parse(releaseJson))
            {
                var update=UpdateService.ParseRelease(document.RootElement,new Version(0,7,0,0));
                Check(update?.DownloadUrl.EndsWith("B5MShot-Setup.exe")==true && update.ChecksumsUrl is not null,"Updater prefers installer and checksum manifest");
                Check(UpdateService.ParseRelease(document.RootElement,new Version(0,8,0,0)) is null,"Equivalent three/four-part versions never repeat an update");
            }
            Check(!UpdateInstaller.IsReleaseAsset("https://evil.example/B5MShot.exe") && !UpdateInstaller.IsReleaseAsset("http://github.com/BU5INESSMAN/B5MShot/releases/download/v0.8.0/B5MShot-Setup.exe"),"Updater rejects insecure or unrelated download origins");
            var digest=new string('a',64);
            Check(UpdateInstaller.ParseChecksum(digest+"  B5MShot-Setup.exe\r\n")==digest,"Checksum parser accepts release manifest");
            var rejected=false;try{UpdateInstaller.ParseChecksum("bad  B5MShot-Setup.exe");}catch(System.IO.InvalidDataException){rejected=true;}
            Check(rejected,"Invalid checksum blocks installation");
            var roundtrip = System.Text.Json.JsonSerializer.Deserialize<B5MShot.App.Models.AppSettings>(System.Text.Json.JsonSerializer.Serialize(new B5MShot.App.Models.AppSettings { ToolbarEdge="Right",ToolbarMonitor="test-monitor" }))!;
            Check(roundtrip.ToolbarEdge=="Right" && roundtrip.ToolbarMonitor=="test-monitor", "Edge and monitor survive settings serialization");
            foreach(var edge in Enum.GetValues<B5MShot.App.Models.HudEdge>())
            {
                var preferences = new B5MShot.App.Models.HudPreferences(edge,"disconnected-monitor");
                var placed = new PreviewWindow(fixture,new UploadService(),hudPreferences:preferences) { Width=900,Height=760 };
                placed.Show(); Pump(100); Call(placed,"SetPaletteOpen",true); Pump(100);
                var root=(FrameworkElement)placed.FindName("Root");
                var toolbar=(FrameworkElement)placed.FindName("HudBar");
                var palette=(FrameworkElement)placed.FindName("PaletteClip");
                Rect Bounds(FrameworkElement element)=>element.TransformToAncestor(root).TransformBounds(new Rect(element.RenderSize));
                var area=new Rect(-1,-1,root.ActualWidth+2,root.ActualHeight+2);
                Check(area.Contains(Bounds(toolbar)) && area.Contains(Bounds(palette)), $"{edge}: toolbar and open palette stay inside editor");
                var orientation=((StackPanel)placed.FindName("ToolPanel")).Orientation;
                Check((orientation==Orientation.Vertical)==preferences.IsVertical,$"{edge}: correct orientation, upright icons");
                Check(PixelsEqual(fixture,(BitmapSource)Call(placed,"RenderFinalImage")!),$"{edge}: export excludes all chrome");
                System.IO.Directory.CreateDirectory("release/edge-qa");
                var bitmap=new RenderTargetBitmap((int)root.ActualWidth,(int)root.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(root);
                var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var file=System.IO.File.Create($"release/edge-qa/{edge}.png"))encoder.Save(file);
                placed.Width=680; placed.Height=480; Pump(100);
                area=new Rect(-1,-1,root.ActualWidth+2,root.ActualHeight+2);
                Check(area.Contains(Bounds(toolbar)) && area.Contains(Bounds(palette)), $"{edge}: small-window bounds remain safe");
                placed.Close();
            }
            AppContext.SetSwitch("B5MShot.DisableAnimations", false);
            var times = new List<double>();
            for (var i = 0; i < 12; i++)
            {
                var watch = Stopwatch.StartNew();
                var image = Task.Run(capture.CaptureVirtualScreen).GetAwaiter().GetResult();
                times.Add(watch.Elapsed.TotalMilliseconds);
                Check(image.IsFrozen && image.PixelWidth > 0, "Background capture is frozen and valid", quiet: true);
            }
            times.Sort();
            Console.WriteLine($"Capture only: median={times[6]:F1} ms, p95={times[11]:F1} ms (12 samples; not hotkey-to-display latency)");
            Console.WriteLine("PASS: smoke suite");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        finally { foreach (Window window in app.Windows.Cast<Window>().ToArray()) window.Close(); }
    }
    private static object? Call(object instance, string name, params object[] arguments) => instance.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(instance, arguments);
    private static void Pump(int milliseconds)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(milliseconds) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start(); Dispatcher.PushFrame(frame);
    }
    private static bool PixelsEqual(BitmapSource left, BitmapSource right)
    {
        if (left.PixelWidth != right.PixelWidth || left.PixelHeight != right.PixelHeight) return false;
        var a = new byte[left.PixelWidth * left.PixelHeight * 4]; var b = new byte[a.Length];
        left.CopyPixels(a, left.PixelWidth * 4, 0); right.CopyPixels(b, right.PixelWidth * 4, 0);
        return a.SequenceEqual(b);
    }
    private static void Check(bool result, string message, bool quiet = false)
    {
        if (!result) throw new InvalidOperationException("FAIL: " + message);
        if (!quiet) Console.WriteLine("PASS: " + message);
    }

}
