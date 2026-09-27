using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using B5MShot.App.Controls;
using B5MShot.App.Services;

namespace B5MShot.App.Views;

public partial class TrayMenuWindow : Window
{
    private readonly Action<string> _action;
    private bool _dismissed;
    public TrayMenuWindow(string shortcut, Action<string> action)
    {
        InitializeComponent(); _action = action; Shortcut.Text = shortcut;
        var symbols = new Dictionary<string,string> { ["screen"]="\uE7F4", ["open"]="\uE8B7", ["settings"]="\uE713", ["updates"]="\uE895", ["help"]="\uE8F2", ["exit"]="\uE7E8" };
        foreach (var button in ((StackPanel)Surface.Child).Children.OfType<System.Windows.Controls.Button>())
        {
            if (button.Content is not string caption || button.Tag is not string tag) continue;
            System.Windows.Automation.AutomationProperties.SetName(button, caption);
            var row = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
            row.Children.Add(new TextBlock { Text = symbols[tag], FontFamily = new System.Windows.Media.FontFamily("Segoe MDL2 Assets"), FontSize = 16, Width = 28, VerticalAlignment = VerticalAlignment.Center, Foreground = button.Foreground });
            row.Children.Add(new TextBlock { Text = caption, Foreground = button.Foreground });
            button.Content = row;
        }
        Loaded += (_, _) => { ScreenPlacement.PositionNearTray(this); CaptureAction.Focus(); };
        ContentRendered += (_, _) => Motion.Reveal(Surface, distance: -9);
        Deactivated += async (_, _) => await DismissAsync();
    }
    private async Task DismissAsync()
    {
        if (_dismissed) return;
        _dismissed = true; IsHitTestVisible = false;
        Motion.To(Surface, OpacityProperty, 0, Motion.Exit, false);
        if (Motion.Enabled) await Task.Delay(Motion.Exit);
        if (IsVisible) Close();
    }
    private void OnAction(object sender, RoutedEventArgs e)
    {
        if (_dismissed || sender is not System.Windows.Controls.Button { Tag: string action }) return;
        _dismissed = true; Close(); _action(action);
    }
    private async void OnMenuKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { e.Handled = true; await DismissAsync(); }
        else if (e.Key is Key.Up or Key.Down)
        {
            e.Handled = true;
            var direction = e.Key == Key.Down ? FocusNavigationDirection.Next : FocusNavigationDirection.Previous;
            (Keyboard.FocusedElement as UIElement)?.MoveFocus(new TraversalRequest(direction));
        }
    }
}
