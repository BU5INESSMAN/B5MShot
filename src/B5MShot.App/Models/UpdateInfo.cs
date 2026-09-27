namespace B5MShot.App.Models;

public sealed record UpdateInfo(
    Version Version,
    string VersionLabel,
    string PageUrl,
    string DownloadUrl,
    string ReleaseNotes,
    string? ChecksumsUrl = null);
