using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
var settings = builder.Configuration.GetSection("B5MShot").Get<ShotSettings>() ?? new ShotSettings();

if (!Uri.TryCreate(settings.PublicBaseUrl, UriKind.Absolute, out var publicUri) || publicUri.Scheme != Uri.UriSchemeHttps)
{
    throw new InvalidOperationException("B5MShot__PublicBaseUrl must be a valid HTTPS URL.");
}

var storagePath = Path.GetFullPath(settings.StoragePath);
Directory.CreateDirectory(storagePath);

builder.Services.Configure<FormOptions>(options => options.MultipartBodyLengthLimit = settings.MaxUploadBytes);
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = settings.MaxUploadBytes);
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
app.UseRateLimiter();

app.MapGet("/", () => Results.Json(new
{
    service = "B5MShot",
    status = "ready",
    upload = "/api/screenshots"
}));

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.MapPost("/api/screenshots", async (HttpRequest request, CancellationToken cancellationToken) =>
{
    if (!request.HasFormContentType)
    {
        return Results.BadRequest(new { error = "multipart_form_required" });
    }

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

    await using var input = file.OpenReadStream();
    var header = new byte[8];
    var bytesRead = await input.ReadAsync(header, cancellationToken);
    input.Position = 0;

    var imageType = DetectImageType(header.AsSpan(0, bytesRead));
    if (imageType is null)
    {
        return Results.BadRequest(new { error = "png_or_jpeg_required" });
    }

    var id = Convert.ToHexString(RandomNumberGenerator.GetBytes(6)).ToLowerInvariant();
    var filePath = Path.Combine(storagePath, $"{id}.{imageType.Extension}");
    await using (var output = new FileStream(filePath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous))
    {
        await input.CopyToAsync(output, cancellationToken);
    }

    var url = $"{settings.PublicBaseUrl.TrimEnd('/')}/i/{id}";
    return Results.Ok(new { id, url });
}).RequireRateLimiting("uploads");

app.MapGet("/i/{id}", (string id, HttpContext context) =>
{
    if (!Regex.IsMatch(id, "^[a-f0-9]{12}$", RegexOptions.CultureInvariant))
    {
        return Results.NotFound();
    }

    foreach (var imageType in ImageTypes.All)
    {
        var filePath = Path.Combine(storagePath, $"{id}.{imageType.Extension}");
        if (!File.Exists(filePath))
        {
            continue;
        }

        context.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
        context.Response.Headers.XContentTypeOptions = "nosniff";
        return Results.File(filePath, imageType.ContentType, enableRangeProcessing: true);
    }

    return Results.NotFound();
});

app.Run();

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
    public long MaxUploadBytes { get; init; } = 15 * 1024 * 1024;
}

public sealed record ImageType(string Extension, string ContentType);

public static class ImageTypes
{
    public static readonly ImageType Png = new("png", "image/png");
    public static readonly ImageType Jpeg = new("jpg", "image/jpeg");
    public static readonly ImageType[] All = [Png, Jpeg];
}
