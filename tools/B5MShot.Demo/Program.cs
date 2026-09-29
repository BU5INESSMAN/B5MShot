using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using B5MShot.App;
using B5MShot.App.Services;
using B5MShot.App.Views;

internal static class Program
{
    [STAThread] static void Main(string[] args)
    {
        if(args.Contains("--verify-update"))
        {
            Task.Run(async()=>{
                using var client=new System.Net.Http.HttpClient();client.DefaultRequestHeaders.UserAgent.ParseAdd("B5MShot-Release-QA/0.8");
                using var document=System.Text.Json.JsonDocument.Parse(await client.GetStringAsync("https://api.github.com/repos/BU5INESSMAN/B5MShot/releases/latest"));
                var update=UpdateService.ParseRelease(document.RootElement,new Version(0,8,3,0)) ?? throw new Exception("Published update not found for 0.8.3");
                if(UpdateService.ParseRelease(document.RootElement,new Version(0,8,4,0)) is not null)throw new Exception("Already current version prompted");
                var path=await UpdateInstaller.DownloadAsync(update,new Progress<int>(),CancellationToken.None);
                Console.WriteLine($"PASS live updater: {update.VersionLabel}; installer downloaded and SHA-256 verified. No installation launched. {path}");
            }).GetAwaiter().GetResult();
            return;
        }
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/B5MShot;component/Themes/Glass.xaml") });
        if (args.Contains("--surfaces"))
        {
            var main = new MainWindow(new SettingsService(), new AutoStartService()) { ShowInTaskbar = true };
            var tray = new TrayMenuWindow("Print Screen", _ => { }) { ShowInTaskbar = true };
            main.Show(); main.Left = 400; main.Top = 100;
            tray.Show(); tray.Left = 860; tray.Top = 240;
            main.Closed += (_, _) => { tray.Close(); app.Shutdown(); };
            app.Run(); return;
        }
        var output = System.IO.Path.GetFullPath(args.FirstOrDefault() ?? "release/demo-frames");
        Directory.CreateDirectory(output);
        var drawing = new DrawingVisual();
        using (var dc = drawing.RenderOpen())
        {
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(228,237,247)), null, new Rect(0,0,1200,680));
            dc.DrawRoundedRectangle(Brushes.White, null, new Rect(65,45,1070,590),24,24);
            void Text(string text,double x,double y,double size, string color) => dc.DrawText(new FormattedText(text,CultureInfo.GetCultureInfo("ru-RU"),FlowDirection.LeftToRight,new Typeface("Segoe UI"),size,(Brush)new BrushConverter().ConvertFromString(color)!,1),new Point(x,y));
            Text("STUDIO / ПЛАН НЕДЕЛИ",105,80,17,"#61758E");
            Text("Главное — в деталях.",105,130,48,"#172A42");
            Text("Пара точных пометок вместо длинного объяснения.",108,207,22,"#61758E");
            for(var i=0;i<3;i++)
            {
                dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(240,245,250)),null,new Rect(105+i*337,280,310,266),18,18);
                Text(new[]{"Дизайн","Разработка","Запуск"}[i],127+i*337,304,22,"#172A42");
                Text(new[]{"Новый интерфейс","Проверить детали","Готово к показу"}[i],127+i*337,352,17,"#61758E");
                dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(20,112,230)),null,new Rect(127+i*337,441,150,43),21,21);
                Text(new[]{"В работе","На проверке","Готово"}[i],147+i*337,450,16,"#FFFFFF");
            }
        }
        var fixture = new RenderTargetBitmap(1200,680,96,96,PixelFormats.Pbgra32); fixture.Render(drawing); fixture.Freeze();
        var editor = new PreviewWindow(fixture,new UploadService()) { Width=1120,Height=780,WindowStyle=WindowStyle.None,ShowInTaskbar=true };
        editor.Show();
        var root = (Grid)editor.FindName("Root");
        root.Background = editor.Background;
        var canvas = (Canvas)editor.FindName("AnnotationCanvas");
        var pen = new SolidColorBrush(Color.FromRgb(237,74,90));
        var line = new Line{X1=900,Y1=236,X2=900,Y2=236,Stroke=pen,StrokeThickness=6,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round};
        var head = new Polygon{Fill=pen};
        var arrow = ((StackPanel)editor.FindName("ToolPanel")).Children.OfType<ToggleButton>().Single(b => (string)b.Tag == "Arrow");
        for(var frame=0;frame<140;frame++)
        {
            if(frame==20) { arrow.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent)); canvas.Children.Add(line); canvas.Children.Add(head); }
            if(frame>=20 && frame<=47)
            {
                var p=(frame-20)/27d; line.X2=900; line.Y2=236+181*p;
                head.Points = new PointCollection{new(900,line.Y2),new(889,line.Y2-19),new(911,line.Y2-19)};
            }
            if(frame==55) Call(editor,"SetPaletteOpen",true);
            if(frame==77) ((Slider)editor.FindName("StrokeSlider")).Value=16;
            if(frame==94) Call(editor,"SetPaletteOpen",false);
            if(frame==108) ((TextBlock)editor.FindName("StatusText")).Text="Готово: Ctrl+C — скопировать, Ctrl+S — сохранить";
            Pump(50);
            var bitmap=new RenderTargetBitmap((int)root.ActualWidth,(int)root.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(root);
            var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream=File.Create(System.IO.Path.Combine(output,$"frame-{frame:000}.png"));encoder.Save(stream);
        }
        editor.Close();Pump(240);
        var settingsWindow = new MainWindow(new SettingsService(),new AutoStartService());
        settingsWindow.Show(); Pump(600);
        SaveVisual((FrameworkElement)settingsWindow.Content,System.IO.Path.Combine(output,"settings.png"));
        settingsWindow.Close();
        var menuWindow = new TrayMenuWindow("Print Screen",_=>{});
        menuWindow.Show();Pump(600);
        SaveVisual((FrameworkElement)menuWindow.Content,System.IO.Path.Combine(output,"tray.png"));
        menuWindow.Close();
        Console.WriteLine("140 frames rendered from the actual WPF editor; synthetic image, no uploads or clipboard changes.");
        app.Shutdown();
    }
    static object? Call(object instance,string method,params object[] args)=>instance.GetType().GetMethod(method,BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(instance,args);
    static void SaveVisual(FrameworkElement visual,string path)
    {
        var bitmap=new RenderTargetBitmap((int)Math.Ceiling(visual.ActualWidth+visual.Margin.Left+visual.Margin.Right),(int)Math.Ceiling(visual.ActualHeight+visual.Margin.Top+visual.Margin.Bottom),96,96,PixelFormats.Pbgra32);bitmap.Render(visual);
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var file=File.Create(path);encoder.Save(file);
    }
    static void Pump(int milliseconds) { var frame=new DispatcherFrame(); var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(milliseconds)};timer.Tick+=(_,_)=>{timer.Stop();frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame); }
}
