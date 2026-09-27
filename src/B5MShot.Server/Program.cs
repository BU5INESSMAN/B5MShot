using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
var settings = builder.Configuration.GetSection("B5MShot").Get<ShotSettings>() ?? new ShotSettings();
var readOnlyUi = builder.Configuration.GetValue<bool>("B5MShot:ReadOnlyUi");

if (!Uri.TryCreate(settings.PublicBaseUrl, UriKind.Absolute, out var publicUri) || publicUri.Scheme != Uri.UriSchemeHttps)
{
    throw new InvalidOperationException("B5MShot__PublicBaseUrl must be a valid HTTPS URL.");
}

settings.Validate();
var storagePath = Path.GetFullPath(settings.StoragePath);
var downloadPath = Path.GetFullPath(settings.DownloadPath);
var setupDownloadPath = Path.GetFullPath(settings.SetupDownloadPath);
var logoPath = Path.Combine(Path.GetDirectoryName(downloadPath)!, "logo.png");
var socialPreviewPath = Path.Combine(Path.GetDirectoryName(downloadPath)!, "og.png");
if (!readOnlyUi) Directory.CreateDirectory(storagePath);
else if (!Directory.Exists(storagePath)) throw new InvalidOperationException("Read-only screenshot mount is missing.");

var requestBodyLimit = checked(settings.MaxUploadBytes + 1024 * 1024);
builder.Services.Configure<FormOptions>(options => options.MultipartBodyLengthLimit = requestBodyLimit);
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = requestBodyLimit);
builder.Services.AddSingleton(settings);
builder.Services.AddSingleton(new StorageManagerOptions(storagePath));
builder.Services.AddSingleton<StorageManager>();
builder.Services.AddSingleton<UploadGate>();
if (!readOnlyUi) builder.Services.AddHostedService<StorageCleanupService>();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("uploads", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 30,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        }));
});

var app = builder.Build();
app.Use(async (context, next) =>
{
    if (readOnlyUi && !HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method))
    {
        context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
        return;
    }
    await next();
});
app.UseStaticFiles(new StaticFileOptions { OnPrepareResponse = ctx =>
{
    ctx.Context.Response.Headers.CacheControl = "public, max-age=3600";
    ctx.Context.Response.Headers.XContentTypeOptions = "nosniff";
} });
app.UseRateLimiter();

app.MapMethods("/", [HttpMethods.Get, HttpMethods.Head], (HttpContext context) =>
{
    context.Response.Headers.XContentTypeOptions = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; style-src 'self'; script-src 'self'; img-src 'self'; font-src 'self'; base-uri 'none'; form-action 'none'; frame-ancestors 'none'";
    return Results.File(Path.Combine(app.Environment.WebRootPath, "landing.html"), "text/html; charset=utf-8");
});

app.MapMethods("/download/B5MShot-preview.exe", [HttpMethods.Get, HttpMethods.Head], () =>
{
    var preview = Path.Combine(Path.GetDirectoryName(downloadPath)!, "B5MShot-preview.exe");
    return File.Exists(preview) ? Results.File(preview, "application/vnd.microsoft.portable-executable", "B5MShot-preview.exe", enableRangeProcessing: true) : Results.NotFound();
});

app.MapGet("/api/info", () => Results.Json(new
{
    service = "B5MShot",
    status = "ready",
    upload = "/api/screenshots",
    retention = "adaptive"
}));

app.MapMethods("/assets/logo.png", [HttpMethods.Get, HttpMethods.Head], (HttpContext context) =>
{
    if (!File.Exists(logoPath))
    {
        return Results.NotFound();
    }

    context.Response.Headers.CacheControl = "public, max-age=86400";
    context.Response.Headers.XContentTypeOptions = "nosniff";
    return Results.File(logoPath, "image/png", lastModified: File.GetLastWriteTimeUtc(logoPath), enableRangeProcessing: true);
});

app.MapMethods("/assets/og.png", [HttpMethods.Get, HttpMethods.Head], (HttpContext context) =>
{
    if (!File.Exists(socialPreviewPath))
    {
        return Results.NotFound();
    }

    context.Response.Headers.CacheControl = "public, max-age=86400";
    context.Response.Headers.XContentTypeOptions = "nosniff";
    return Results.File(socialPreviewPath, "image/png", lastModified: File.GetLastWriteTimeUtc(socialPreviewPath), enableRangeProcessing: true);
});

app.MapGet("/robots.txt", () => Results.Text("User-agent: *\nAllow: /\n", "text/plain"));

app.MapGet("/health", (StorageManager storage) =>
{
    var status = storage.GetStatus();
    return Results.Ok(new
    {
        status = "healthy",
        storageBytes = status.KnownStorageBytes,
        freeDiskBytes = status.FreeDiskBytes,
        maxStorageBytes = settings.MaxStorageBytes,
        minFreeDiskBytes = settings.MinFreeDiskBytes
    });
});

app.MapMethods("/download/B5MShot.exe", [HttpMethods.Get, HttpMethods.Head], (HttpContext context) =>
{
    if (!File.Exists(downloadPath))
    {
        return Results.NotFound(new { error = "download_not_ready" });
    }

    context.Response.Headers.CacheControl = "public, max-age=300, must-revalidate";
    context.Response.Headers.XContentTypeOptions = "nosniff";
    return Results.File(
        downloadPath,
        "application/vnd.microsoft.portable-executable",
        fileDownloadName: "B5MShot.exe",
        lastModified: File.GetLastWriteTimeUtc(downloadPath),
        enableRangeProcessing: true);
});

app.MapMethods("/download/B5MShot-Setup.exe", [HttpMethods.Get, HttpMethods.Head], (HttpContext context) =>
{
    if (!File.Exists(setupDownloadPath))
    {
        return Results.NotFound(new { error = "setup_download_not_ready" });
    }

    context.Response.Headers.CacheControl = "public, max-age=300, must-revalidate";
    context.Response.Headers.XContentTypeOptions = "nosniff";
    return Results.File(
        setupDownloadPath,
        "application/vnd.microsoft.portable-executable",
        fileDownloadName: "B5MShot-Setup.exe",
        lastModified: File.GetLastWriteTimeUtc(setupDownloadPath),
        enableRangeProcessing: true);
});

app.MapPost("/api/screenshots", async (HttpRequest request, StorageManager storage, UploadGate uploadGate, CancellationToken cancellationToken) =>
{
    if (!request.HasFormContentType)
    {
        return Results.BadRequest(new { error = "multipart_form_required" });
    }

    if (!uploadGate.TryEnter())
    {
        return Results.Json(new { error = "server_busy_try_again" }, statusCode: StatusCodes.Status429TooManyRequests);
    }

    try
    {
        var form = await request.ReadFormAsync(cancellationToken);
        var file = form.Files.GetFile("file");
        if (file is null || file.Length == 0)
        {
            return Results.BadRequest(new { error = "file_required" });
        }

        if (file.Length > settings.MaxUploadBytes)
        {
            return Results.Json(new { error = "file_too_large" }, statusCode: StatusCodes.Status413PayloadTooLarge);
        }

        if (!await storage.EnsureCapacityAsync(file.Length, cancellationToken))
        {
            return Results.Json(new { error = "storage_temporarily_full" }, statusCode: 507);
        }

        await using var input = file.OpenReadStream();
        var header = new byte[8];
        var bytesRead = await input.ReadAsync(header, cancellationToken);
        input.Position = 0;

        var imageType = DetectImageType(header.AsSpan(0, bytesRead));
        if (imageType is null)
        {
            return Results.BadRequest(new { error = "png_or_jpeg_required" });
        }

        var storedFile = storage.CreateFile(imageType.Extension);
        try
        {
            await using (var output = new FileStream(
                storedFile.TemporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await input.CopyToAsync(output, cancellationToken);
                await output.FlushAsync(cancellationToken);
            }

            File.Move(storedFile.TemporaryPath, storedFile.FinalPath);
            storage.RecordStored(file.Length);
        }
        catch
        {
            if (File.Exists(storedFile.TemporaryPath))
            {
                File.Delete(storedFile.TemporaryPath);
            }
            throw;
        }

        var url = $"{settings.PublicBaseUrl.TrimEnd('/')}/i/{storedFile.Id}";
        return Results.Ok(new { id = storedFile.Id, url });
    }
    finally
    {
        uploadGate.Exit();
    }
}).RequireRateLimiting("uploads");

app.MapGet("/i/{id}", (string id, HttpContext context, StorageManager storage) =>
{
    var screenshot = FindScreenshot(id, storage);
    if (screenshot is null)
    {
        return Results.NotFound();
    }

    context.Response.Headers.CacheControl = "public, max-age=300";
    context.Response.Headers.XContentTypeOptions = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; style-src 'self' 'unsafe-inline'; script-src 'unsafe-inline'; img-src 'self' blob:; connect-src 'self'; font-src 'self'; base-uri 'none'; form-action 'none'; frame-ancestors 'none'";
    return Results.Content(ScreenshotPage.Render(id, screenshot.Value.Type, new FileInfo(screenshot.Value.Path).Length), "text/html; charset=utf-8");
});

app.MapMethods("/raw/{id}", [HttpMethods.Get, HttpMethods.Head], (string id, HttpContext context, StorageManager storage) =>
{
    var screenshot = FindScreenshot(id, storage);
    if (screenshot is null)
    {
        return Results.NotFound();
    }

    var shouldDownload = bool.TryParse(context.Request.Query["download"], out var download) && download;
    context.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
    context.Response.Headers.XContentTypeOptions = "nosniff";
    return Results.File(
        screenshot.Value.Path,
        screenshot.Value.Type.ContentType,
        fileDownloadName: shouldDownload ? $"B5MShot-{id}.{screenshot.Value.Type.Extension}" : null,
        enableRangeProcessing: true);
});

app.Run();

static (string Path, ImageType Type)? FindScreenshot(string id, StorageManager storage)
{
    if (!Regex.IsMatch(id, "^(?:[a-f0-9]{12}|[a-f0-9]{24})$", RegexOptions.CultureInvariant))
    {
        return null;
    }

    foreach (var imageType in ImageTypes.All)
    {
        var filePath = storage.FindFile(id, imageType.Extension);
        if (filePath is not null)
        {
            return (filePath, imageType);
        }
    }

    return null;
}

static ImageType? DetectImageType(ReadOnlySpan<byte> header)
{
    ReadOnlySpan<byte> png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    if (header.StartsWith(png))
    {
        return ImageTypes.Png;
    }

    return header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF
        ? ImageTypes.Jpeg
        : null;
}

public sealed class ShotSettings
{
    public string PublicBaseUrl { get; init; } = "https://s.bu5inessman.ru";
    public string StoragePath { get; init; } = "/data/screenshots";
    public string DownloadPath { get; init; } = "/data/downloads/B5MShot.exe";
    public string SetupDownloadPath { get; init; } = "/data/downloads/B5MShot-Setup.exe";
    public long MaxUploadBytes { get; init; } = 15 * 1024 * 1024;
    public long MaxStorageBytes { get; init; } = 70L * 1024 * 1024 * 1024;
    public long CleanupTargetBytes { get; init; } = 65L * 1024 * 1024 * 1024;
    public long MinFreeDiskBytes { get; init; } = 20L * 1024 * 1024 * 1024;
    public long CleanupTargetFreeDiskBytes { get; init; } = 25L * 1024 * 1024 * 1024;
    public int CleanupIntervalMinutes { get; init; } = 30;
    public int MaxConcurrentUploads { get; init; } = 4;

    public void Validate()
    {
        if (MaxUploadBytes <= 0 || MaxStorageBytes <= 0 || CleanupTargetBytes <= 0 || CleanupTargetBytes >= MaxStorageBytes)
        {
            throw new InvalidOperationException("B5MShot storage size limits are invalid.");
        }
        if (MinFreeDiskBytes <= 0 || CleanupTargetFreeDiskBytes <= MinFreeDiskBytes)
        {
            throw new InvalidOperationException("B5MShot free disk limits are invalid.");
        }
        if (CleanupIntervalMinutes is < 5 or > 1440)
        {
            throw new InvalidOperationException("B5MShot cleanup interval must be between 5 and 1440 minutes.");
        }
        if (MaxConcurrentUploads is < 1 or > 32)
        {
            throw new InvalidOperationException("B5MShot concurrent upload limit must be between 1 and 32.");
        }
    }
}

public sealed class UploadGate
{
    private readonly SemaphoreSlim _slots;

    public UploadGate(ShotSettings settings) => _slots = new SemaphoreSlim(settings.MaxConcurrentUploads, settings.MaxConcurrentUploads);

    public bool TryEnter() => _slots.Wait(0);

    public void Exit() => _slots.Release();
}

public sealed record ImageType(string Extension, string ContentType);

public static class ImageTypes
{
    public static readonly ImageType Png = new("png", "image/png");
    public static readonly ImageType Jpeg = new("jpg", "image/jpeg");
    public static readonly ImageType[] All = [Png, Jpeg];
}
