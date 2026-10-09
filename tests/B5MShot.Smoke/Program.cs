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
            const string family = "BU5INESSMAN.B5MShot_mdepjvqy5n31g";
            var packageCommand = AutoStartService.BuildCommand(@"C:\Program Files\WindowsApps\old-version\B5MShot.exe",family,@"C:\Windows");
            Check(packageCommand == "\"C:\\Windows\\explorer.exe\" \"shell:AppsFolder\\BU5INESSMAN.B5MShot_mdepjvqy5n31g!B5MShot\"", "Packaged startup uses stable activation identity");
            Check(packageCommand == AutoStartService.BuildCommand(@"C:\Program Files\WindowsApps\new-version\B5MShot.exe",family,@"C:\Windows"), "App update does not change startup command");
            Check(AutoStartService.BuildCommand(@"C:\My Shots\B5MShot.exe",null,@"C:\Windows")=="\"C:\\My Shots\\B5MShot.exe\"", "Portable startup still quotes executable paths with spaces");
            var invalidFamily=false;
            try { AutoStartService.BuildCommand("app.exe","invalid\" family",@"C:\Windows"); } catch(InvalidOperationException) { invalidFamily=true; }
            Check(invalidFamily,"Startup rejects unexpected package identity");
            Check(AutoStartService.CommandForRepair(packageCommand,null,@"C:\Windows") is null,"Running a portable copy cannot hijack installed startup");
            Check(AutoStartService.CommandForRepair(null,family,@"C:\Windows") is null,"Startup repair never enables disabled autostart");
            Check(AutoStartService.CommandForRepair("old.exe",family,@"C:\Windows")==packageCommand,"Installed startup repair replaces legacy path");
            CheckUpdateDownloads();
            if(args.Contains("--autostart")) return 0;
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
            Check(((ToggleButton)window.FindName("MoveToolButton")).Visibility == Visibility.Collapsed, "Imported image does not offer unavailable desktop crop controls");
            window.Close(); Pump(260);
            Check(!window.IsVisible, "Editor exits after its short closing animation");
            var inline = new PreviewWindow(crop, new UploadService(), fixture, new Rect(.125, .185185, .75, .62963));
            inline.Show(); Pump(650);
            Check(inline.WindowStyle == WindowStyle.None && !inline.ShowInTaskbar, "Inline editor uses desktop overlay");
            Check(PixelsEqual(crop, (BitmapSource)Call(inline, "RenderFinalImage")!), "Inline export excludes selection outline, desktop and HUD");
            var inlineFrame = (SelectionOutline)inline.FindName("SelectionFrame");
            Check(Math.Abs(inlineFrame.ActualWidth - inline.ActualWidth * .75) < 1, "Inline outline follows selected desktop region");
            Check(inlineFrame.ShowHandles && ((ToggleButton)inline.FindName("MoveToolButton")).IsChecked == true, "Desktop crop has resize handles and defaults to move");
            var originalRegion = new Int32Rect(120, 100, 720, 340);
            var newRegion = new Int32Rect(70, 60, 810, 400);
            Call(inline, "ApplySelection", newRegion); Pump(60);
            Check(PixelsEqual(new CroppedBitmap(fixture, newRegion), (BitmapSource)Call(inline, "RenderFinalImage")!), "Expanded crop exports newly revealed source pixels, without scaling or chrome");
            var annotation = new System.Windows.Shapes.Rectangle { Width=20,Height=20,Fill=Brushes.Red };
            var annotations = (Canvas)inline.FindName("AnnotationCanvas");
            Canvas.SetLeft(annotation, 15); Canvas.SetTop(annotation, 25); annotations.Children.Add(annotation);
            var annotated = (BitmapSource)Call(inline, "RenderFinalImage")!;
            Call(inline, "ApplySelection", originalRegion); Pump(40);
            Check(Canvas.GetLeft(annotation)==-35 && Canvas.GetTop(annotation)==-15, "Annotations remain anchored to desktop pixels during resize");
            Call(inline, "ApplySelection", newRegion); Pump(40);
            Check(PixelsEqual(annotated, (BitmapSource)Call(inline, "RenderFinalImage")!), "Shrinking then expanding restores off-crop annotations exactly");
            annotations.Children.Clear();
            var bounds = (Rect)Call(inline,"SelectionBounds")!;
            Check((SelectionEdge)Call(inline,"HitSelection",bounds.TopLeft)! == (SelectionEdge.Left|SelectionEdge.Top), "Corner hit test selects two-axis resize");
            Check((SelectionEdge)Call(inline,"HitSelection",new Point(bounds.Left+bounds.Width/2,bounds.Top))! == SelectionEdge.Top, "Edge hit test selects one-axis resize");
            var center = new Point(bounds.Left+bounds.Width/2,bounds.Top+bounds.Height/2);
            Check((SelectionEdge)Call(inline,"HitSelection",center)! == SelectionEdge.Move, "Interior drag moves in selection tool");
            Call(inline,"BeginSelectionDrag",SelectionEdge.Move,center);
            Call(inline,"UpdateSelectionDrag",new Point(center.X+35,center.Y+20)); Pump(40);
            Call(inline,"EndSelectionDrag",true); Pump(40);
            Check(PixelsEqual(new CroppedBitmap(fixture,newRegion),(BitmapSource)Call(inline,"RenderFinalImage")!),"Escape/capture-loss rollback restores exact crop");
            Call(inline,"ToolButton_Click",inline.FindName("PenToolButton"),new RoutedEventArgs());
            Check((SelectionEdge)Call(inline,"HitSelection",center)! == SelectionEdge.None,"Drawing tool keeps interior drawing gestures");
            Check((SelectionEdge)Call(inline,"HitSelection",bounds.TopLeft)! == (SelectionEdge.Left|SelectionEdge.Top),"Resize remains available while drawing tool is selected");
            Check(SelectionGeometry.Change(originalRegion,SelectionEdge.Move,-10000,10000,960,540)==new Int32Rect(0,200,720,340),"Move clamps to desktop without changing size");
            foreach(var edge in new[]{SelectionEdge.Left,SelectionEdge.Top,SelectionEdge.Right,SelectionEdge.Bottom,
                SelectionEdge.Left|SelectionEdge.Top,SelectionEdge.Right|SelectionEdge.Top,SelectionEdge.Left|SelectionEdge.Bottom,SelectionEdge.Right|SelectionEdge.Bottom})
            foreach(var delta in new[]{-10000,10000})
            {
                var changed=SelectionGeometry.Change(originalRegion,edge,delta,delta,960,540);
                Check(changed.X>=0 && changed.Y>=0 && changed.Width>=4 && changed.Height>=4 && changed.X+changed.Width<=960 && changed.Y+changed.Height<=540,$"{edge}: resize cannot cross or escape source ({delta})");
            }
            Call(inline,"ApplySelection",new Int32Rect(0,0,960,540)); Pump(50);
            Check(PixelsEqual(fixture,(BitmapSource)Call(inline,"RenderFinalImage")!),"Selection can expand to the full original desktop");
            System.IO.Directory.CreateDirectory("release/selection-qa");
            var selectionRoot=(FrameworkElement)inline.FindName("Root");
            Call(inline,"ApplySelection",originalRegion); Pump(150);
            var selectionPreview=new RenderTargetBitmap((int)selectionRoot.ActualWidth,(int)selectionRoot.ActualHeight,96,96,PixelFormats.Pbgra32);selectionPreview.Render(selectionRoot);
            var selectionEncoder=new PngBitmapEncoder();selectionEncoder.Frames.Add(BitmapFrame.Create(selectionPreview));
            using(var file=System.IO.File.Create("release/selection-qa/editor.png"))selectionEncoder.Save(file);
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
            Check(((TextBlock)settings.FindName("VersionText")).Text.Contains("0.8.6"), "Settings displays release version");
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
                var sv=(FrameworkElement)placed.FindName("SvField");
                var hit=root.InputHitTest(sv.TranslatePoint(new Point(sv.ActualWidth/2,sv.ActualHeight/2),root)) as DependencyObject;
                while(hit is not null && !ReferenceEquals(hit,palette)) hit=VisualTreeHelper.GetParent(hit);
                Check(ReferenceEquals(hit,palette),$"{edge}: overflowing palette receives pointer input");
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
                var monitor=System.Windows.Forms.Screen.AllScreens.Last();
                var fixedPreferences=preferences with {MonitorDeviceName=monitor.DeviceName};
                var desktopEditor=new PreviewWindow(crop,new UploadService(),fixture,new Rect(.125,.185185,.75,.62963),fixedPreferences);
                desktopEditor.Show(); Pump(100); Call(desktopEditor,"SetPaletteOpen",true); Pump(100);
                var desktopRoot=(FrameworkElement)desktopEditor.FindName("Root");
                var desktopBar=(FrameworkElement)desktopEditor.FindName("HudBar");
                var desktopPalette=(FrameworkElement)desktopEditor.FindName("PaletteClip");
                var desktopMonitor=ScreenPlacement.MonitorBounds(desktopEditor,monitor);desktopMonitor.Inflate(1,1);
                Check(desktopMonitor.Contains(desktopBar.TransformToAncestor(desktopRoot).TransformBounds(new Rect(desktopBar.RenderSize)))
                    && desktopMonitor.Contains(desktopPalette.TransformToAncestor(desktopRoot).TransformBounds(new Rect(desktopPalette.RenderSize))),$"{edge}: extra move tool and palette fit selected monitor");
                Call(desktopEditor,"ApplySelection",new Int32Rect(0,0,960,540)); Pump(50);
                Check(PixelsEqual(fixture,(BitmapSource)Call(desktopEditor,"RenderFinalImage")!),$"{edge}: resized crop exports correctly with palette open");
                desktopEditor.Close();
                var selector=new SelectionWindow(fixture,capture,fixedPreferences);selector.Show();Pump(100);
                var hint=(FrameworkElement)selector.FindName("SelectionHud");
                var captureArea=(FrameworkElement)selector.FindName("CaptureArea");
                var monitorBounds=ScreenPlacement.MonitorBounds(selector,monitor);monitorBounds.Inflate(1,1);
                var hintBounds=hint.TransformToAncestor(captureArea).TransformBounds(new Rect(hint.RenderSize));
                Check(monitorBounds.Contains(hintBounds),$"{edge}: hint attaches to requested monitor");
                selector.Close();
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
    private static void CheckUpdateDownloads()
    {
        var fixture = System.IO.Path.GetTempFileName();
        try
        {
            var bytes = new byte[] { 1, 2, 3, 4 };
            System.IO.File.WriteAllBytes(fixture, bytes);
            var expectedHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes));
            var verifyCache = typeof(UpdateInstaller).GetMethod("HasExpectedChecksumAsync", BindingFlags.NonPublic | BindingFlags.Static)!;
            bool CacheMatches() => ((Task<bool>)verifyCache.Invoke(null, new object[] { fixture, expectedHash, CancellationToken.None })!).GetAwaiter().GetResult();
            Check(CacheMatches(), "Prepared installer cache requires matching SHA-256");
            System.IO.File.WriteAllBytes(fixture, new byte[] { 4, 3, 2, 1 });
            Check(!CacheMatches(), "Modified cached installer is rejected before reuse");
            var update = new B5MShot.App.Models.UpdateInfo(new Version(0, 8, 6), "v0.8.6", "page", "installer", "notes", "checksums");
            var pending = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            var requests = 0;
            CancellationToken sharedToken = default;
            using (var downloads = new UpdateDownloadService((_, token) => { requests++; sharedToken = token; return pending.Task; }))
            {
                var background = downloads.PrepareAsync(update);
                using var windowLifetime = new CancellationTokenSource();
                var windowWait = downloads.GetInstallerAsync(update, windowLifetime.Token);
                var secondWait = downloads.GetInstallerAsync(update, CancellationToken.None);
                Check(requests == 1, "Background and update windows share one download");
                windowLifetime.Cancel();
                try { windowWait.GetAwaiter().GetResult(); throw new Exception("Window wait was not canceled"); }
                catch (OperationCanceledException) { }
                Check(!sharedToken.IsCancellationRequested && !background.IsCompleted, "Closing an update window does not cancel the background download");
                pending.SetResult(fixture);
                Check(secondWait.GetAwaiter().GetResult() == fixture, "Another update window still receives the prepared installer");
                Check(downloads.GetInstallerAsync(update, CancellationToken.None).GetAwaiter().GetResult() == fixture && requests == 1,
                    "Reopening uses the prepared installer without another download");
                downloads.Invalidate(update);
                downloads.PrepareAsync(update).GetAwaiter().GetResult();
                Check(requests == 2, "Invalid installer can be downloaded again");
                var changed = update with { DownloadUrl = "replacement" };
                downloads.PrepareAsync(changed).GetAwaiter().GetResult();
                Check(requests == 3, "Changed release assets do not share stale downloads");
                System.IO.File.Delete(fixture);
                downloads.PrepareAsync(changed).GetAwaiter().GetResult();
                Check(requests == 4, "Removed cached installer is downloaded again");
            }
            Check(sharedToken.IsCancellationRequested, "Application shutdown cancels background downloads");
            var attempts = 0;
            using var retry = new UpdateDownloadService((_, _) => ++attempts == 1
                ? Task.FromException<string>(new System.IO.IOException("offline")) : Task.FromResult(fixture));
            try { retry.GetInstallerAsync(update, CancellationToken.None).GetAwaiter().GetResult(); }
            catch (System.IO.IOException) { }
            Check(retry.GetInstallerAsync(update, CancellationToken.None).GetAwaiter().GetResult() == fixture && attempts == 2,
                "Failed background downloads can be retried");
        }
        finally { System.IO.File.Delete(fixture); }
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
