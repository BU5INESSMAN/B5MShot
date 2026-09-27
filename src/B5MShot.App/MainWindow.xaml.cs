using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using B5MShot.App.Models;
using B5MShot.App.Services;
using B5MShot.App.Controls;
using System.Windows.Media;

namespace B5MShot.App;

public partial class MainWindow : Window
{
    private readonly SettingsService _settingsService;
    private readonly AutoStartService _autoStartService;
    private HotKeyGesture _areaGesture = HotKeyGesture.Disabled;
    private HotKeyGesture _fullscreenGesture = HotKeyGesture.Disabled;
    private HotKeyTarget _captureTarget;
    private int _visibilityGeneration;

    public MainWindow(SettingsService settingsService, AutoStartService autoStartService)
    {
        InitializeComponent();
        _settingsService = settingsService;
        _autoStartService = autoStartService;
        VersionText.Text = $"Версия {Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "0.7.0"}";
        RefreshFromSettings();
        IsVisibleChanged += (_, _) =>
        {
            if (!IsVisible) return;
            _visibilityGeneration++;
            Dispatcher.BeginInvoke(() => Motion.Reveal(Shell, distance: -12));
        };
    }

    public void RefreshFromSettings()
    {
        var preferences = HudPreferences.FromSettings(_settingsService.Current);
        foreach (var button in EdgeChoices.Children.OfType<System.Windows.Controls.RadioButton>()) button.IsChecked = (string)button.Tag == preferences.Edge.ToString();
        var monitors = new List<MonitorChoice> { new("", "Автоматически — монитор с курсором") };
        monitors.AddRange(System.Windows.Forms.Screen.AllScreens.Select((screen,index) => new MonitorChoice(screen.DeviceName,
            $"Монитор {index+1} · {screen.Bounds.Width} × {screen.Bounds.Height}{(screen.Primary ? " · основной" : "")}")));
        ToolbarMonitorBox.ItemsSource = monitors;
        ToolbarMonitorBox.SelectedValue = monitors.Any(m=>m.DeviceName==preferences.MonitorDeviceName) ? preferences.MonitorDeviceName : "";
        _areaGesture = HotKeyGesture.Parse(_settingsService.Current.AreaCaptureHotKey);
        _fullscreenGesture = HotKeyGesture.Parse(_settingsService.Current.FullscreenCaptureHotKey);
        StartWithWindowsCheckBox.IsChecked = _autoStartService.IsEnabled();
        RefreshHotKeyLabels();
    }

    public void SetStatus(string message, bool isError = false)
    {
        StatusText.Text = message;
        StatusText.Foreground = isError
            ? System.Windows.Media.Brushes.Orange
            : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(92, 211, 155));
    }

    private void CaptureButton_Click(object sender, RoutedEventArgs e) => ((App)System.Windows.Application.Current).BeginCaptureArea();

    private void CaptureFullscreenButton_Click(object sender, RoutedEventArgs e) => ((App)System.Windows.Application.Current).BeginCaptureFullscreen();

    private async void OpenImageButton_Click(object sender, RoutedEventArgs e) => await ((App)System.Windows.Application.Current).ChooseAndUploadImageAsync();

    private void AutoStartToggleChanged(object sender, RoutedEventArgs e)
    {
        StartWithWindowsCheckBox?.ApplyTemplate();
        if (StartWithWindowsCheckBox?.Template.FindName("SwitchOffset", StartWithWindowsCheckBox) is TranslateTransform offset)
            Motion.To(offset, TranslateTransform.XProperty, StartWithWindowsCheckBox.IsChecked == true ? 18 : 0, 260);
    }

    private void AreaHotKeyButton_Click(object sender, RoutedEventArgs e) => BeginHotKeyCapture(HotKeyTarget.Area);

    private void FullscreenHotKeyButton_Click(object sender, RoutedEventArgs e) => BeginHotKeyCapture(HotKeyTarget.Fullscreen);

    private void BeginHotKeyCapture(HotKeyTarget target)
    {
        _captureTarget = target;
        AreaHotKeyButton.Content = target == HotKeyTarget.Area ? "Нажмите сочетание…" : _areaGesture.DisplayName;
        FullscreenHotKeyButton.Content = target == HotKeyTarget.Fullscreen ? "Нажмите сочетание…" : _fullscreenGesture.DisplayName;
        SetStatus("Ожидаю нажатие. Esc — отмена, Delete — отключить.");
        Focus();
        Keyboard.Focus(target == HotKeyTarget.Area ? AreaHotKeyButton : FullscreenHotKeyButton);
    }

    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (_captureTarget == HotKeyTarget.None)
        {
            if (e.Key == Key.Escape) { _ = HideAnimatedAsync(); e.Handled = true; }
            return;
        }

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.Escape)
        {
            CancelHotKeyCapture();
            e.Handled = true;
            return;
        }

        if (key is Key.Delete or Key.Back)
        {
            SetCapturedGesture(HotKeyGesture.Disabled);
            e.Handled = true;
            return;
        }

        var gesture = HotKeyGesture.FromKey(key, Keyboard.Modifiers);
        if (gesture is null)
        {
            SetStatus("Добавьте к модификатору обычную клавишу.", true);
            e.Handled = true;
            return;
        }

        SetCapturedGesture(gesture);
        e.Handled = true;
    }

    private void SetCapturedGesture(HotKeyGesture gesture)
    {
        if (_captureTarget == HotKeyTarget.Area)
        {
            _areaGesture = gesture;
        }
        else if (_captureTarget == HotKeyTarget.Fullscreen)
        {
            _fullscreenGesture = gesture;
        }

        _captureTarget = HotKeyTarget.None;
        RefreshHotKeyLabels();
        SetStatus("Комбинация выбрана. Нажмите «Сохранить».");
    }

    private void CancelHotKeyCapture()
    {
        _captureTarget = HotKeyTarget.None;
        RefreshHotKeyLabels();
        SetStatus("Изменение отменено.");
    }

    private void RefreshHotKeyLabels()
    {
        AreaHotKeyButton.Content = _areaGesture.DisplayName;
        FullscreenHotKeyButton.Content = _fullscreenGesture.DisplayName;
        AreaShortcutText.Text = _areaGesture.DisplayName;
        FullscreenShortcutText.Text = _fullscreenGesture.DisplayName;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_areaGesture.IsDisabled && _areaGesture.StorageValue == _fullscreenGesture.StorageValue)
        {
            SetStatus("Для двух действий нужны разные сочетания.", true);
            return;
        }

        try
        {
            var startWithWindows = StartWithWindowsCheckBox.IsChecked == true;
            _autoStartService.SetEnabled(startWithWindows);
            _settingsService.Save(new AppSettings
            {
                AreaCaptureHotKey = _areaGesture.StorageValue,
                FullscreenCaptureHotKey = _fullscreenGesture.StorageValue,
                StartWithWindows = startWithWindows,
                ToolbarEdge = (string?)EdgeChoices.Children.OfType<System.Windows.Controls.RadioButton>().FirstOrDefault(b=>b.IsChecked==true)?.Tag ?? "Top",
                ToolbarMonitor = ToolbarMonitorBox.SelectedValue as string ?? ""
            });
            var error = ((App)System.Windows.Application.Current).ApplyHotKeys();
            SetStatus(error ?? "Настройки сохранены", error is not null);
        }
        catch (Exception exception)
        {
            ErrorLogService.Write(exception, "Saving settings");
            SetStatus("Не удалось сохранить настройки.", true);
        }
    }

    private async void UpdateButton_Click(object sender, RoutedEventArgs e)
    {
        UpdateButton.IsEnabled = false;
        SetStatus("Проверяю обновления…");
        try
        {
            await ((App)System.Windows.Application.Current).CheckForUpdatesAsync(userInitiated: true);
        }
        finally
        {
            UpdateButton.IsEnabled = true;
        }
    }

    private void ReportBugButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo("https://t.me/BU5INESSMAN") { UseShellExecute = true });
        }
        catch (Exception exception)
        {
            ErrorLogService.Write(exception, "Opening bug report contact");
            SetStatus("Не удалось открыть Telegram: t.me/BU5INESSMAN", true);
        }
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private async void CloseButton_Click(object sender, RoutedEventArgs e) => await HideAnimatedAsync();

    public async Task HideAnimatedAsync()
    {
        var generation = ++_visibilityGeneration;
        Motion.To(Shell, OpacityProperty, 0, Motion.Exit, false);
        if (Motion.Enabled) await Task.Delay(Motion.Exit);
        if (generation == _visibilityGeneration) Hide();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (System.Windows.Application.Current is App { IsShuttingDown: false })
        {
            e.Cancel = true;
            _ = HideAnimatedAsync();
            return;
        }

        base.OnClosing(e);
    }

    private enum HotKeyTarget
    {
        None,
        Area,
        Fullscreen
    }
    private sealed record MonitorChoice(string DeviceName, string Label);
}
