using System.Security.Cryptography;

public sealed record StorageManagerOptions(string StoragePath);

public sealed record StoredFile(string Id, string TemporaryPath, string FinalPath);

public sealed record StorageStatus(long KnownStorageBytes, long FreeDiskBytes);

public sealed class StorageManager
{
    private readonly ShotSettings _settings;
    private readonly string _storagePath;
    private readonly ILogger<StorageManager> _logger;
    private readonly SemaphoreSlim _cleanupLock = new(1, 1);
    private long _knownStorageBytes = -1;

    public StorageManager(ShotSettings settings, StorageManagerOptions options, ILogger<StorageManager> logger)
    {
        _settings = settings;
        _storagePath = options.StoragePath;
        _logger = logger;
    }

    public StoredFile CreateFile(string extension)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var id = Convert.ToHexString(RandomNumberGenerator.GetBytes(12)).ToLowerInvariant();
            var directory = Path.Combine(_storagePath, id[..2]);
            Directory.CreateDirectory(directory);
            var finalPath = Path.Combine(directory, $"{id}.{extension}");
            var temporaryPath = finalPath + ".uploading";
            if (!File.Exists(finalPath) && !File.Exists(temporaryPath))
            {
                return new StoredFile(id, temporaryPath, finalPath);
            }
        }

        throw new IOException("Could not allocate a unique screenshot id.");
    }

    public string? FindFile(string id, string extension)
    {
        if (id.Length >= 2)
        {
            var nestedPath = Path.Combine(_storagePath, id[..2], $"{id}.{extension}");
            if (File.Exists(nestedPath))
            {
                return nestedPath;
            }
        }

        var legacyPath = Path.Combine(_storagePath, $"{id}.{extension}");
        return File.Exists(legacyPath) ? legacyPath : null;
    }

    public async Task<bool> EnsureCapacityAsync(long incomingBytes, CancellationToken cancellationToken)
    {
        var status = GetStatus();
        if (status.FreeDiskBytes - incomingBytes >= _settings.MinFreeDiskBytes &&
            (status.KnownStorageBytes < 0 || status.KnownStorageBytes + incomingBytes <= _settings.MaxStorageBytes))
        {
            return true;
        }

        await CleanupAsync(cancellationToken, incomingBytes);
        status = GetStatus();
        return status.FreeDiskBytes - incomingBytes >= _settings.MinFreeDiskBytes &&
               status.KnownStorageBytes + incomingBytes <= _settings.MaxStorageBytes;
    }

    public void RecordStored(long bytes)
    {
        while (true)
        {
            var current = Interlocked.Read(ref _knownStorageBytes);
            var updated = current < 0 ? bytes : checked(current + bytes);
            if (Interlocked.CompareExchange(ref _knownStorageBytes, updated, current) == current)
            {
                return;
            }
        }
    }

    public StorageStatus GetStatus()
    {
        var freeBytes = GetDrive().AvailableFreeSpace;
        return new StorageStatus(Interlocked.Read(ref _knownStorageBytes), freeBytes);
    }

    public async Task CleanupAsync(CancellationToken cancellationToken, long incomingBytes = 0)
    {
        await _cleanupLock.WaitAsync(cancellationToken);
        try
        {
            DeleteAbandonedUploads();
            var files = EnumerateStoredFiles();
            var storageBytes = files.Sum(file => file.Length);
            Interlocked.Exchange(ref _knownStorageBytes, storageBytes);

            var freeDiskBytes = GetDrive().AvailableFreeSpace;
            if (storageBytes + incomingBytes <= _settings.MaxStorageBytes &&
                freeDiskBytes - incomingBytes >= _settings.MinFreeDiskBytes)
            {
                return;
            }

            _logger.LogWarning(
                "Storage cleanup started. Screenshots: {StorageBytes} bytes; free disk: {FreeBytes} bytes",
                storageBytes,
                freeDiskBytes);

            var deletedFiles = 0;
            long deletedBytes = 0;
            foreach (var file in files.OrderBy(file => file.LastWriteTimeUtc))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (storageBytes + incomingBytes <= _settings.CleanupTargetBytes &&
                    freeDiskBytes - incomingBytes >= _settings.CleanupTargetFreeDiskBytes)
                {
                    break;
                }

                try
                {
                    var length = file.Length;
                    file.Delete();
                    storageBytes -= length;
                    freeDiskBytes += length;
                    deletedBytes += length;
                    deletedFiles++;
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    _logger.LogWarning(exception, "Could not delete old screenshot {Path}", file.FullName);
                }
            }

            Interlocked.Exchange(ref _knownStorageBytes, storageBytes);
            _logger.LogInformation(
                "Storage cleanup completed. Deleted {FileCount} files ({DeletedBytes} bytes); screenshots left: {StorageBytes} bytes",
                deletedFiles,
                deletedBytes,
                storageBytes);
        }
        finally
        {
            _cleanupLock.Release();
        }
    }

    private List<FileInfo> EnumerateStoredFiles() => Directory
        .EnumerateFiles(_storagePath, "*", SearchOption.AllDirectories)
        .Where(path => path.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase))
        .Select(path => new FileInfo(path))
        .ToList();

    private void DeleteAbandonedUploads()
    {
        var cutoff = DateTime.UtcNow.AddHours(-1);
        foreach (var path in Directory.EnumerateFiles(_storagePath, "*.uploading", SearchOption.AllDirectories))
        {
            try
            {
                if (File.GetLastWriteTimeUtc(path) < cutoff)
                {
                    File.Delete(path);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning(exception, "Could not delete abandoned upload {Path}", path);
            }
        }
    }

    private DriveInfo GetDrive()
    {
        var root = Path.GetPathRoot(_storagePath) ?? "/";
        return new DriveInfo(root);
    }
}

public sealed class StorageCleanupService : BackgroundService
{
    private readonly StorageManager _storage;
    private readonly ShotSettings _settings;
    private readonly ILogger<StorageCleanupService> _logger;

    public StorageCleanupService(StorageManager storage, ShotSettings settings, ILogger<StorageCleanupService> logger)
    {
        _storage = storage;
        _settings = settings;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RunCleanupAsync(stoppingToken);
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(_settings.CleanupIntervalMinutes));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunCleanupAsync(stoppingToken);
        }
    }

    private async Task RunCleanupAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _storage.CleanupAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal application shutdown.
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Scheduled storage cleanup failed");
        }
    }
}
