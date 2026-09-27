using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using B5MShot.App.Models;

namespace B5MShot.App.Services;

public sealed class UpdateService
{
    private static readonly Uri LatestReleaseEndpoint = new("https://api.github.com/repos/BU5INESSMAN/B5MShot/releases/latest");
    private static readonly HttpClient Client = CreateClient();

    public Version CurrentVersion { get; } = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 4, 0);

    public async Task<UpdateInfo?> GetAvailableUpdateAsync(CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseEndpoint);
        using var response = await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new InvalidOperationException("Выпуск обновления пока недоступен.");
        }

        response.EnsureSuccessStatusCode();
        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(responseStream, cancellationToken: cancellationToken);
        var root = document.RootElement;
        return ParseRelease(root, CurrentVersion);
    }

    public static UpdateInfo? ParseRelease(JsonElement root, Version currentVersion)
    {
        if (root.TryGetProperty("draft", out var draft) && draft.GetBoolean())
        {
            return null;
        }
        if (root.TryGetProperty("prerelease", out var prerelease) && prerelease.GetBoolean()) return null;

        var tag = root.GetProperty("tag_name").GetString()?.Trim() ?? string.Empty;
        var normalizedTag = tag.TrimStart('v', 'V');
        if (!Version.TryParse(normalizedTag, out var latestVersion) || Normalize(latestVersion) <= Normalize(currentVersion))
        {
            return null;
        }

        var pageUrl = root.GetProperty("html_url").GetString() ?? "https://github.com/BU5INESSMAN/B5MShot/releases/latest";
        var releaseNotes = root.TryGetProperty("body", out var body)
            ? body.GetString() ?? "В новой версии есть улучшения и исправления."
            : "В новой версии есть улучшения и исправления.";
        var downloadUrl = FindWindowsDownload(root) ?? pageUrl;
        return new UpdateInfo(latestVersion, string.IsNullOrWhiteSpace(tag) ? latestVersion.ToString(3) : tag, pageUrl, downloadUrl, releaseNotes, FindAsset(root,"SHA256SUMS.txt"));
    }

    private static Version Normalize(Version version) => new(version.Major,version.Minor,Math.Max(0,version.Build),Math.Max(0,version.Revision));

    private static string? FindAsset(JsonElement root,string filename) => root.TryGetProperty("assets",out var assets) && assets.ValueKind==JsonValueKind.Array
        ? assets.EnumerateArray().Where(a=>a.TryGetProperty("name",out var name) && string.Equals(name.GetString(),filename,StringComparison.OrdinalIgnoreCase))
            .Select(a=>a.TryGetProperty("browser_download_url",out var url)?url.GetString():null).FirstOrDefault() : null;

    private static string? FindWindowsDownload(JsonElement root)
    {
        var installer = FindAsset(root,"B5MShot-Setup.exe");
        if (installer is not null) return installer;
        if (!root.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var asset in assets.EnumerateArray())
        {
            var name = asset.TryGetProperty("name", out var nameNode) ? nameNode.GetString() : null;
            if (name?.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) != true)
            {
                continue;
            }

            return asset.TryGetProperty("browser_download_url", out var urlNode) ? urlNode.GetString() : null;
        }

        return null;
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("B5MShot-Updater/0.8");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }
}
