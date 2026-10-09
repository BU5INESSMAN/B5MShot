using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using B5MShot.App.Infrastructure;
using B5MShot.App.Models;
using B5MShot.App.Services;
using B5MShot.App.Views;
using Forms = System.Windows.Forms;

namespace B5MShot.App;

public partial class App : System.Windows.Application
{
    private readonly SettingsService _settingsService = new();
    private readonly CaptureService _captureService = new();
    private readonly GlobalHotKey _hotKeys = new();
    private readonly UpdateService _updateService = new();
    private readonly UpdateDownloadService _updateDownloads = new();
    private readonly AutoStartService _autoStartService = new();
    private readonly ShellIntegrationService _shellIntegrationService = new();
    private UploadService _uploadService = null!;
    private Forms.NotifyIcon? _trayIcon;
    private TrayMenuWindow? _trayMenu;
    private Icon? _trayAppIcon;
    private Mutex? _singleInstanceMutex;
    private UpdateHandoffService? _updateHandoff;
    private bool _captureActive;
    private bool _choosingImage;
    private bool _checkingForUpdates;
    private readonly DispatcherTimer _updateTimer = new() { Interval = TimeSpan.FromHours(1) };
    private Version? _notifiedUpdate;
    private UpdateInfo? _availableUpdate;
    private UpdateWindow? _updateWindow;
    public bool HasActiveEditor => _captureActive || Windows.OfType<PreviewWindow>().Any() || Windows.OfType<SelectionWindow>().Any();

    public bool IsShuttingDown { get; private set; }
    public MainWindow MainAppWindow { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        RegisterErrorHandlers();

        var uploadPath = GetUploadPath(e.Args);

        _singleInstanceMutex = new Mutex(initiallyOwned: true, @"Local\B5MShot.SingleInstance", out var isFirstInstance);
        if (!isFirstInstance && uploadPath is null)
            isFirstInstance = UpdateHandoffService.ReplaceOlderInstance(_singleInstanceMutex);
        if (!isFirstInstance)
        {
            if (uploadPath is not null)
            {
                _settingsService.Load();
                _uploadService = new UploadService();
                Dispatcher.BeginInvoke(async () =>
                {
                    await OpenImageFileInEditorAsync(uploadPath);
                    Shutdown();
                }, DispatcherPriority.ApplicationIdle);
                return;
            }

            if (!e.Args.Contains("--autostart"))
                System.Windows.MessageBox.Show("B5MShot уже работает. Если в предыдущей версии открыт редактор, сохраните снимок и повторите запуск.", "B5MShot", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        try
        {
            _settingsService.Load();
            // An OS setting failure must never prevent capture/hotkeys/tray from starting.
            try
            {
                _autoStartService.RepairExistingRegistration();
            }
            catch (Exception exception) { ErrorLogService.Write(exception, "Repairing autostart registration"); }
            _uploadService = new UploadService();
            _shellIntegrationService.ConfigureForCurrentInstallation();
            MainAppWindow = new MainWindow(_settingsService, _autoStartService);
            MainWindow = MainAppWindow;
            _hotKeys.Initialize(MainAppWindow);
            _updateHandoff = new UpdateHandoffService(this, MainAppWindow);
            ApplyHotKeys();
            CreateTrayIcon();
            _updateTimer.Tick += async (_,_) => await CheckForUpdatesAsync(userInitiated:false);
            _updateTimer.Start();

            if (e.Args.Any(argument => argument.Equals("--settings", StringComparison.OrdinalIgnoreCase)))
            {
                Dispatcher.BeginInvoke((Action)ShowSettings, DispatcherPriority.ApplicationIdle);
            }

            if (uploadPath is not null)
            {
                Dispatcher.BeginInvoke(async () => await OpenImageFileInEditorAsync(uploadPath), DispatcherPriority.ApplicationIdle);
            }

            Dispatcher.BeginInvoke(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(6));
                await CheckForUpdatesAsync(userInitiated: false);
            }, DispatcherPriority.ApplicationIdle);
        }
        catch (Exception exception)
        {
            ErrorLogService.Write(exception, "Application startup");
            System.Windows.MessageBox.Show("Не удалось запустить B5MShot. Подробности записаны в журнал ошибок.", "B5MShot", MessageBoxButton.OK, MessageBoxImage.Error);
            IsShuttingDown = true;
            Shutdown();
        }
    }

    public string? ApplyHotKeys()
    {
        _hotKeys.Clear();
        var errors = new List<string>();

        TryRegisterHotKey(0, HotKeyGesture.Parse(_settingsService.Current.AreaCaptureHotKey), BeginCaptureArea, errors);
        TryRegisterHotKey(1, HotKeyGesture.Parse(_settingsService.Current.FullscreenCaptureHotKey), BeginCaptureFullscreen, errors);

        var error = errors.Count == 0 ? null : string.Join(" ", errors);
        MainAppWindow.SetStatus(error ?? "Работает в трее", error is not null);
        return error;
    }

    public async Task CheckForUpdatesAsync(bool userInitiated)
    {
        if (IsShuttingDown) return;
        if (userInitiated && _availableUpdate is not null)
        {
            ShowUpdateWindow(_availableUpdate);
            return;
        }
        if (_checkingForUpdates)
        {
            return;
        }

        _checkingForUpdates = true;
        try
        {
            var update = await _updateService.GetAvailableUpdateAsync();
            if (update is null)
            {
                if (userInitiated)
                {
                    MainAppWindow.SetStatus("У вас установлена последняя версия.");
                }
                return;
            }

            _availableUpdate = update;
            _ = PrepareUpdateAsync(update);
            if (userInitiated) ShowUpdateWindow(update);
        }
        catch (Exception exception)
        {
            ErrorLogService.Write(exception, "Checking for updates");
            if (userInitiated)
            {
                MainAppWindow.SetStatus("Не удалось проверить обновления. Проверьте интернет.", true);
            }
        }
        finally
        {
            _checkingForUpdates = false;
        }
    }

    private async Task PrepareUpdateAsync(UpdateInfo update)
    {
        var ready = false;
        try
        {
            await _updateDownloads.PrepareAsync(update);
            ready = true;
        }
        catch (OperationCanceledException) { return; }
        catch (Exception exception) { ErrorLogService.Write(exception, "Downloading update in background"); }
        if (IsShuttingDown || _availableUpdate != update || _notifiedUpdate == update.Version) return;
        _notifiedUpdate = update.Version;
        if (_trayIcon is null) return;
        _trayIcon.Text = $"B5MShot — доступна версия {update.Version.ToString(3)}";
        if (_updateWindow is { IsVisible: true }) return;
        _trayIcon.ShowBalloonTip(10000, $"B5MShot {update.VersionLabel}",
            ready ? "Обновление скачано. Нажмите, чтобы установить, когда закончите редактирование."
                  : "Доступно обновление. Нажмите, чтобы скачать и установить.", Forms.ToolTipIcon.Info);
    }

    private void ShowUpdateWindow(UpdateInfo update)
    {
        if (IsShuttingDown) return;
        if (_updateWindow is { IsVisible: true })
        {
            _updateWindow.Activate();
            return;
        }
        _updateWindow = new UpdateWindow(_updateService.CurrentVersion, update, _updateDownloads);
        if (MainAppWindow.IsVisible) _updateWindow.Owner = MainAppWindow;
        try { _updateWindow.ShowDialog(); }
        finally { _updateWindow = null; }
    }

    public void BeginCapture() => BeginCaptureArea();

    public void BeginCaptureArea() => StartCapture(fullscreen: false);

    public void BeginCaptureFullscreen() => StartCapture(fullscreen: true);

    public async Task ChooseAndUploadImageAsync()
    {
        if (IsShuttingDown) return;
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Выберите изображение для редактирования в B5MShot",
            Filter = "Изображения|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff|Все файлы|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        bool chosen;
        _choosingImage = true;
        try { chosen = dialog.ShowDialog() == true; }
        finally { _choosingImage = false; }
        if (chosen)
        {
            await OpenImageFileInEditorAsync(dialog.FileName);
        }
    }

    private void StartCapture(bool fullscreen)
    {
        if (_captureActive || IsShuttingDown)
        {
            return;
        }

        var timing = System.Diagnostics.Stopwatch.StartNew();
        _captureActive = true;
        _trayMenu?.Close();
        var settingsWereVisible = MainAppWindow.IsVisible;
        MainAppWindow.Hide();
        foreach (var resultHud in Windows.OfType<ResultHudWindow>().ToArray())
        {
            settingsWereVisible |= resultHud.IsVisible;
            resultHud.Hide();
            resultHud.Close();
        }

        Dispatcher.BeginInvoke(async () =>
        {
            try
            {
                if (settingsWereVisible)
                    await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
                var screenshot = await Task.Run(() =>
                {
                    ScreenPlacement.FlushComposition();
                    return _captureService.CaptureVirtualScreen();
                });
                if (fullscreen)
                {
                    var preview = new PreviewWindow(screenshot, _uploadService, screenshot, new Rect(0, 0, 1, 1), HudPreferences.FromSettings(_settingsService.Current));
                    preview.ContentRendered += (_, _) => ErrorLogService.CaptureTiming(timing.ElapsedMilliseconds, screenshot.PixelWidth, screenshot.PixelHeight);
                    preview.Closed += (_, _) => _captureActive = false;
                    preview.Show();
                    return;
                }

                var preferences = HudPreferences.FromSettings(_settingsService.Current);
                var selection = new SelectionWindow(screenshot, _captureService, preferences);
                var editing = false;
                selection.CaptureFinished += (_, image) =>
                {
                    var preview = new PreviewWindow(image, _uploadService, screenshot, selection.SelectedRegion, preferences with { MonitorDeviceName = selection.HudMonitorDeviceName });
                    preview.Closed += (_, _) => _captureActive = false;
                    preview.Show();
                    editing = true;
                };
                selection.Closed += (_, _) => { if (!editing) _captureActive = false; };
                selection.ContentRendered += (_, _) => ErrorLogService.CaptureTiming(timing.ElapsedMilliseconds, screenshot.PixelWidth, screenshot.PixelHeight);
                selection.Show();
                selection.Activate();
            }
            catch (Exception exception)
            {
                _captureActive = false;
                ErrorLogService.Write(exception, "Capturing screenshot");
                ShowSettings();
                System.Windows.MessageBox.Show("Не удалось сделать снимок. Подробности записаны в журнал ошибок.", "B5MShot", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }, DispatcherPriority.Normal);
    }

    public void ToggleSettings()
    {
        if (MainAppWindow.IsVisible)
        {
            _ = MainAppWindow.HideAnimatedAsync();
        }
        else
        {
            ShowSettings();
        }
    }

    public void ShowSettings()
    {
        _trayMenu?.Close();
        MainAppWindow.RefreshFromSettings();
        MainAppWindow.Show();
        MainAppWindow.WindowState = WindowState.Normal;
        ScreenPlacement.PositionNearTray(MainAppWindow);
        MainAppWindow.Activate();
    }

    public void ExitApplication()
    {
        IsShuttingDown = true;
        _hotKeys.Dispose();
        _trayIcon?.Dispose();
        _trayMenu?.Close();
        _trayAppIcon?.Dispose();
        MainAppWindow.Close();
        Shutdown();
    }

    public bool ReserveUpdateShutdown()
    {
        if (HasActiveEditor || _choosingImage) return false;
        IsShuttingDown = true;
        _hotKeys.Clear();
        return true;
    }

    private void TryRegisterHotKey(int slot, HotKeyGesture gesture, Action action, ICollection<string> errors)
    {
        try
        {
            _hotKeys.Register(slot, gesture, action);
        }
        catch (Exception exception)
        {
            ErrorLogService.Write(exception, $"Registering hotkey {gesture.StorageValue}");
            errors.Add(exception.Message);
        }
    }

    private void CreateTrayIcon()
    {
        _trayAppIcon = Environment.ProcessPath is { } executablePath
            ? Icon.ExtractAssociatedIcon(executablePath)
            : null;

        _trayIcon = new Forms.NotifyIcon
        {
            Text = "B5MShot — работает в фоне",
            Icon = _trayAppIcon ?? SystemIcons.Application,
            Visible = true
        };
        _trayIcon.BalloonTipClicked += (_, _) => Dispatcher.Invoke(() =>
        {
            if (_availableUpdate is not null) ShowUpdateWindow(_availableUpdate);
        });
        _trayIcon.MouseClick += (_, eventArgs) =>
        {
            if (eventArgs.Button == Forms.MouseButtons.Left)
            {
                Dispatcher.Invoke(ToggleSettings);
            }
            else if (eventArgs.Button == Forms.MouseButtons.Right)
            {
                Dispatcher.Invoke(() =>
                {
                    if (_trayMenu is { IsVisible: true }) { _trayMenu.Close(); return; }
                    _trayMenu = new TrayMenuWindow(HotKeyGesture.Parse(_settingsService.Current.AreaCaptureHotKey).DisplayName, async action =>
                    {
                        try
                        {
                            switch (action)
                            {
                                case "area": BeginCaptureArea(); break;
                                case "screen": BeginCaptureFullscreen(); break;
                                case "open": await ChooseAndUploadImageAsync(); break;
                                case "settings": ShowSettings(); break;
                                case "updates": await CheckForUpdatesAsync(true); break;
                                case "help": System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://t.me/BU5INESSMAN") { UseShellExecute = true }); break;
                                case "exit": ExitApplication(); break;
                            }
                        }
                        catch (Exception exception) { ErrorLogService.Write(exception, "Tray action"); }
                    });
                    _trayMenu.Show(); _trayMenu.Activate();
                });
            }
        };
    }

    private async Task OpenImageFileInEditorAsync(string filePath)
    {
        if (IsShuttingDown) return;
        try
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("Выбранное изображение не найдено.", filePath);
            }

            var file = new FileInfo(filePath);
            if (file.Length > 50L * 1024 * 1024)
            {
                throw new InvalidOperationException("Файл слишком большой. Максимальный размер исходного изображения — 50 МБ.");
            }

            BitmapImage image;
            await using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = stream;
                image.EndInit();
                image.Freeze();
            }

            if (image.PixelWidth <= 0 || image.PixelHeight <= 0 || (long)image.PixelWidth * image.PixelHeight > 100_000_000)
            {
                throw new InvalidOperationException("Изображение имеет неподдерживаемый размер.");
            }

            var editorClosed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var editor = new PreviewWindow(image, _uploadService, hudPreferences: HudPreferences.FromSettings(_settingsService.Current));
            editor.Closed += (_, _) => editorClosed.TrySetResult();
            editor.Show();
            editor.Activate();
            await editorClosed.Task;
            foreach (var hud in Windows.OfType<ResultHudWindow>().ToArray())
            {
                var hudClosed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                hud.Closed += (_, _) => hudClosed.TrySetResult();
                await hudClosed.Task;
            }
        }
        catch (Exception exception)
        {
            ErrorLogService.Write(exception, $"Opening image file editor: {filePath}");
            System.Windows.MessageBox.Show(
                $"Не удалось открыть изображение в редакторе.\n\n{exception.Message}",
                "B5MShot",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private static string? GetUploadPath(IReadOnlyList<string> arguments)
    {
        for (var index = 0; index < arguments.Count - 1; index++)
        {
            if (arguments[index].Equals("--upload", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(arguments[index + 1]))
            {
                try
                {
                    return Path.GetFullPath(arguments[index + 1]);
                }
                catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
                {
                    ErrorLogService.Write(exception, "Reading Explorer upload path");
                    return null;
                }
            }
        }

        return null;
    }

    private void RegisterErrorHandlers()
    {
        DispatcherUnhandledException += (_, eventArgs) =>
        {
            ErrorLogService.Write(eventArgs.Exception, "Unhandled UI exception");
            eventArgs.Handled = true;
            System.Windows.MessageBox.Show("Произошла ошибка. B5MShot продолжит работу, а подробности сохранены в журнал.", "B5MShot", MessageBoxButton.OK, MessageBoxImage.Warning);
        };
        TaskScheduler.UnobservedTaskException += (_, eventArgs) =>
        {
            ErrorLogService.Write(eventArgs.Exception, "Unobserved task exception");
            eventArgs.SetObserved();
        };
        AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
        {
            if (eventArgs.ExceptionObject is Exception exception)
            {
                ErrorLogService.Write(exception, "Unhandled application exception");
            }
        };
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _updateHandoff?.Dispose();
        _updateTimer.Stop();
        _updateDownloads.Dispose();
        _trayIcon?.Dispose();
        _trayAppIcon?.Dispose();
        _hotKeys.Dispose();
        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }
}
