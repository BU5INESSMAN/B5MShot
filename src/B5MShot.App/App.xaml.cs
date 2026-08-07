using System.Drawing;
using System.Threading;
using System.Windows;
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
    private readonly AutoStartService _autoStartService = new();
    private UploadService _uploadService = null!;
    private Forms.NotifyIcon? _trayIcon;
    private Icon? _trayAppIcon;
    private Mutex? _singleInstanceMutex;
    private bool _captureActive;
    private bool _checkingForUpdates;

    public bool IsShuttingDown { get; private set; }
    public MainWindow MainAppWindow { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        RegisterErrorHandlers();

        _singleInstanceMutex = new Mutex(initiallyOwned: true, @"Local\B5MShot.SingleInstance", out var isFirstInstance);
        if (!isFirstInstance)
        {
            System.Windows.MessageBox.Show("B5MShot уже запущен и находится в системном трее.", "B5MShot", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        try
        {
            _settingsService.Load();
            _uploadService = new UploadService();
            MainAppWindow = new MainWindow(_settingsService, _autoStartService);
            MainWindow = MainAppWindow;
            _hotKeys.Initialize(MainAppWindow);
            ApplyHotKeys();
            CreateTrayIcon();

            if (e.Args.Any(argument => argument.Equals("--settings", StringComparison.OrdinalIgnoreCase)))
            {
                Dispatcher.BeginInvoke((Action)ShowSettings, DispatcherPriority.ApplicationIdle);
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
        if (_checkingForUpdates || IsShuttingDown)
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

            var window = new UpdateWindow(_updateService.CurrentVersion, update);
            if (MainAppWindow.IsVisible)
            {
                window.Owner = MainAppWindow;
            }
            window.ShowDialog();
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

    public void BeginCapture() => BeginCaptureArea();

    public void BeginCaptureArea() => StartCapture(fullscreen: false);

    public void BeginCaptureFullscreen() => StartCapture(fullscreen: true);

    private void StartCapture(bool fullscreen)
    {
        if (_captureActive)
        {
            return;
        }

        _captureActive = true;
        MainAppWindow.Hide();

        Dispatcher.BeginInvoke(async () =>
        {
            await Task.Delay(120);
            try
            {
                var screenshot = _captureService.CaptureVirtualScreen();
                if (fullscreen)
                {
                    var preview = new PreviewWindow(screenshot, _uploadService);
                    preview.Closed += (_, _) => _captureActive = false;
                    preview.Show();
                    return;
                }

                var selection = new SelectionWindow(screenshot, _captureService);
                selection.CaptureFinished += (_, image) => new PreviewWindow(image, _uploadService).Show();
                selection.Closed += (_, _) => _captureActive = false;
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
        }, DispatcherPriority.ApplicationIdle);
    }

    public void ToggleSettings()
    {
        if (MainAppWindow.IsVisible)
        {
            MainAppWindow.Hide();
        }
        else
        {
            ShowSettings();
        }
    }

    public void ShowSettings()
    {
        MainAppWindow.RefreshFromSettings();
        MainAppWindow.Show();
        MainAppWindow.WindowState = WindowState.Normal;
        var workArea = SystemParameters.WorkArea;
        MainAppWindow.Left = Math.Max(workArea.Left + 12, workArea.Right - MainAppWindow.Width - 12);
        MainAppWindow.Top = Math.Max(workArea.Top + 12, workArea.Bottom - MainAppWindow.Height - 12);
        MainAppWindow.Activate();
    }

    public void ExitApplication()
    {
        IsShuttingDown = true;
        _hotKeys.Dispose();
        _trayIcon?.Dispose();
        _trayAppIcon?.Dispose();
        MainAppWindow.Close();
        Shutdown();
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
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Выделить область", null, (_, _) => Dispatcher.Invoke(BeginCaptureArea));
        menu.Items.Add("Весь экран", null, (_, _) => Dispatcher.Invoke(BeginCaptureFullscreen));
        menu.Items.Add("Настройки", null, (_, _) => Dispatcher.Invoke(ShowSettings));
        menu.Items.Add("Проверить обновления", null, (_, _) => Dispatcher.Invoke(async () => await CheckForUpdatesAsync(true)));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Выход", null, (_, _) => Dispatcher.Invoke(ExitApplication));

        _trayAppIcon = Environment.ProcessPath is { } executablePath
            ? Icon.ExtractAssociatedIcon(executablePath)
            : null;

        _trayIcon = new Forms.NotifyIcon
        {
            Text = "B5MShot — работает в фоне",
            Icon = _trayAppIcon ?? SystemIcons.Application,
            Visible = true,
            ContextMenuStrip = menu
        };
        _trayIcon.MouseClick += (_, eventArgs) =>
        {
            if (eventArgs.Button == Forms.MouseButtons.Left)
            {
                Dispatcher.Invoke(ToggleSettings);
            }
        };
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
        _trayIcon?.Dispose();
        _trayAppIcon?.Dispose();
        _hotKeys.Dispose();
        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }
}
