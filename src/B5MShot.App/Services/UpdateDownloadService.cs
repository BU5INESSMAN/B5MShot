using System.IO;
using B5MShot.App.Models;

namespace B5MShot.App.Services;

/// <summary>Shares background downloads with update dialogs; closing a dialog only cancels its wait.</summary>
public sealed class UpdateDownloadService : IDisposable
{
    private readonly CancellationTokenSource _lifetime = new();
    private readonly Dictionary<(Version, string, string?), Task<string>> _downloads = new();
    private readonly Func<UpdateInfo, CancellationToken, Task<string>> _download;

    public UpdateDownloadService(Func<UpdateInfo, CancellationToken, Task<string>>? download = null)
    {
        _download = download ?? ((update, token) => UpdateInstaller.DownloadAsync(update, new Progress<int>(), token));
    }

    public Task<string> PrepareAsync(UpdateInfo update)
    {
        lock (_downloads)
        {
            _lifetime.Token.ThrowIfCancellationRequested();
            var key = (update.Version, update.DownloadUrl, update.ChecksumsUrl);
            if (_downloads.TryGetValue(key, out var task) && !task.IsFaulted && !task.IsCanceled &&
                (!task.IsCompletedSuccessfully || File.Exists(task.Result))) return task;
            return _downloads[key] = _download(update, _lifetime.Token);
        }
    }

    public Task<string> GetInstallerAsync(UpdateInfo update, CancellationToken token) => PrepareAsync(update).WaitAsync(token);

    public void Invalidate(UpdateInfo update)
    {
        lock (_downloads) _downloads.Remove((update.Version, update.DownloadUrl, update.ChecksumsUrl));
    }

    public void Dispose()
    {
        _lifetime.Cancel();
        _lifetime.Dispose();
    }
}
