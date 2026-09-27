using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using B5MShot.App.Models;
using B5MShot.App.Services;

namespace B5MShot.App.Views;

public partial class UpdateWindow : Window
{
    private readonly UpdateInfo _update;
    private readonly CancellationTokenSource _download = new();
    private bool _downloading;

    public UpdateWindow(Version currentVersion, UpdateInfo update)
    {
        InitializeComponent();
        _update = update;
        Closed += (_,_) => _download.Cancel();
        CurrentVersionText.Text = currentVersion.ToString(3);
        NewVersionText.Text = update.VersionLabel;
        ReleaseNotesText.Text = string.IsNullOrWhiteSpace(update.ReleaseNotes)
            ? "Улучшения стабильности и исправления ошибок."
            : update.ReleaseNotes;
    }

    private async void DownloadButton_Click(object sender, RoutedEventArgs e)
    {
        if (_downloading) return;
        if (System.Windows.Application.Current is App { HasActiveEditor: true })
        {
            ReleaseNotesText.Text = "Сначала сохраните снимок и закройте редактор. После этого можно обновить приложение.";
            return;
        }
        if(System.Windows.MessageBox.Show(this,"Скачать и установить обновление? Windows попросит подтвердить установку. Приложение перезапустится; настройки сохранятся.","Обновление B5MShot",MessageBoxButton.OKCancel,MessageBoxImage.Question)!=MessageBoxResult.OK) return;
        _downloading = true; InstallButton.IsEnabled = false;
        try
        {
            var installer=await UpdateInstaller.DownloadAsync(_update,new Progress<int>(value=>InstallButton.Content=$"Загрузка {value}%"),_download.Token);
            _download.Token.ThrowIfCancellationRequested();
            if(System.Windows.Application.Current is App { HasActiveEditor: true })
            {
                ReleaseNotesText.Text="Обновление скачано. Сохраните открытый снимок, закройте редактор и повторите установку.";
                return;
            }
            Process.Start(new ProcessStartInfo(installer) { UseShellExecute = true });
            ((App)System.Windows.Application.Current).ExitApplication();
        }
        catch(OperationCanceledException) { if(IsVisible && !_download.IsCancellationRequested) ReleaseNotesText.Text="Загрузка заняла слишком много времени. Проверьте соединение и повторите попытку."; }
        catch (Exception exception)
        {
            ErrorLogService.Write(exception, "Installing update");
            if(IsVisible) ReleaseNotesText.Text="Не удалось завершить обновление или установка отменена. Проверьте интернет и повторите попытку.\n\n"+exception.Message;
        }
        finally { _downloading=false; InstallButton.IsEnabled=true; InstallButton.Content="Обновить"; }
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
