using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using B5MShot.App.Models;
using B5MShot.App.Services;

namespace B5MShot.App.Views;

public partial class UpdateWindow : Window
{
    private readonly UpdateInfo _update;

    public UpdateWindow(Version currentVersion, UpdateInfo update)
    {
        InitializeComponent();
        _update = update;
        CurrentVersionText.Text = currentVersion.ToString(3);
        NewVersionText.Text = update.VersionLabel;
        ReleaseNotesText.Text = string.IsNullOrWhiteSpace(update.ReleaseNotes)
            ? "Улучшения стабильности и исправления ошибок."
            : update.ReleaseNotes;
    }

    private void DownloadButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(_update.DownloadUrl) { UseShellExecute = true });
            DialogResult = true;
        }
        catch (Exception exception)
        {
            ErrorLogService.Write(exception, "Opening update URL");
            System.Windows.MessageBox.Show(this, $"Не удалось открыть ссылку.\n{_update.PageUrl}", "B5MShot", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void LaterButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }
}
