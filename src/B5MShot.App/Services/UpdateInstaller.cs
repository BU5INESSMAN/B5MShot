using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using B5MShot.App.Models;

namespace B5MShot.App.Services;

public static class UpdateInstaller
{
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromMinutes(10) };
    private const long MaximumBytes = 256L * 1024 * 1024;
    public static bool IsReleaseAsset(string? url) => Uri.TryCreate(url,UriKind.Absolute,out var uri)
        && uri.Scheme=="https" && uri.Host.Equals("github.com",StringComparison.OrdinalIgnoreCase) && uri.IsDefaultPort
        && string.IsNullOrEmpty(uri.UserInfo) && uri.AbsolutePath.StartsWith("/BU5INESSMAN/B5MShot/releases/download/",StringComparison.OrdinalIgnoreCase);

    public static string ParseChecksum(string manifest)
    {
        var matches=Regex.Matches(manifest,@"(?m)^([a-fA-F0-9]{64})[ \t]+\*?B5MShot-Setup\.exe\r?$");
        if(matches.Count!=1) throw new InvalidDataException("Нет однозначной контрольной суммы установщика.");
        return matches[0].Groups[1].Value;
    }

    public static async Task<string> DownloadAsync(UpdateInfo update,IProgress<int> progress,CancellationToken token)
    {
        using var lifetime=CancellationTokenSource.CreateLinkedTokenSource(token);
        lifetime.CancelAfter(TimeSpan.FromMinutes(10));
        token=lifetime.Token;
        if(!IsReleaseAsset(update.DownloadUrl) || !IsReleaseAsset(update.ChecksumsUrl)
           || !new Uri(update.DownloadUrl).AbsolutePath.EndsWith("/B5MShot-Setup.exe",StringComparison.Ordinal))
            throw new InvalidDataException("Обновление не содержит проверяемый установщик.");
        using var checksumResponse=await Client.GetAsync(update.ChecksumsUrl,HttpCompletionOption.ResponseHeadersRead,token);
        checksumResponse.EnsureSuccessStatusCode();
        await using var checksumStream=await checksumResponse.Content.ReadAsStreamAsync(token);
        var checksumBytes=new byte[65537];
        var checksumLength=await checksumStream.ReadAtLeastAsync(checksumBytes,checksumBytes.Length,false,token);
        if(checksumLength>65536) throw new InvalidDataException("Слишком большой файл контрольных сумм.");
        var expected=ParseChecksum(System.Text.Encoding.UTF8.GetString(checksumBytes,0,checksumLength));
        var directory=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"B5MShot","updates",update.Version.ToString());
        Directory.CreateDirectory(directory);
        var destination=Path.Combine(directory,"B5MShot-Setup.exe");
        if (await HasExpectedChecksumAsync(destination, expected, token))
        {
            progress.Report(100);
            return destination;
        }
        var temporary=Path.Combine(directory,Guid.NewGuid().ToString("N")+".partial");
        try
        {
            using var response=await Client.GetAsync(update.DownloadUrl,HttpCompletionOption.ResponseHeadersRead,token);
            response.EnsureSuccessStatusCode();
            var length=response.Content.Headers.ContentLength;
            if(length is > MaximumBytes or <= 0) throw new InvalidDataException("Некорректный размер обновления.");
            await using var input=await response.Content.ReadAsStreamAsync(token);
            using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            long total=0; var buffer=new byte[65536];
            await using(var output=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None,65536,true))
            {
                int count;
                while((count=await input.ReadAsync(buffer,token))>0)
                {
                    total+=count;
                    if(total>MaximumBytes) throw new InvalidDataException("Обновление превышает допустимый размер.");
                    hash.AppendData(buffer,0,count);
                    await output.WriteAsync(buffer.AsMemory(0,count),token);
                    progress.Report(length is > 0 ? (int)Math.Min(99,total*100/length.Value) : 0);
                }
            }
            if(total==0 || (length.HasValue && total!=length.Value)
               || !Convert.ToHexString(hash.GetHashAndReset()).Equals(expected,StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Контрольная сумма не совпала. Скачайте обновление повторно.");
            token.ThrowIfCancellationRequested();
            File.Move(temporary,destination,overwrite:true);
            progress.Report(100);
            return destination;
        }
        finally { if(File.Exists(temporary)) File.Delete(temporary); }
    }

    public static async Task VerifyAsync(UpdateInfo update, string installer, CancellationToken token)
    {
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(token);
        lifetime.CancelAfter(TimeSpan.FromMinutes(10));
        token = lifetime.Token;
        if (!IsReleaseAsset(update.ChecksumsUrl)) throw new InvalidDataException("Нет проверяемой контрольной суммы обновления.");
        using var response = await Client.GetAsync(update.ChecksumsUrl, HttpCompletionOption.ResponseHeadersRead, token);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(token);
        var bytes = new byte[65537];
        var length = await stream.ReadAtLeastAsync(bytes, bytes.Length, false, token);
        if (length > 65536) throw new InvalidDataException("Слишком большой файл контрольных сумм.");
        var expected = ParseChecksum(System.Text.Encoding.UTF8.GetString(bytes, 0, length));
        if (!await HasExpectedChecksumAsync(installer, expected, token))
            throw new InvalidDataException("Контрольная сумма не совпала. Скачайте обновление повторно.");
    }

    private static async Task<bool> HasExpectedChecksumAsync(string path, string expected, CancellationToken token)
    {
        if (!File.Exists(path)) return false;
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, true);
        if (stream.Length is <= 0 or > MaximumBytes) return false;
        var hash = await SHA256.HashDataAsync(stream, token);
        return Convert.ToHexString(hash).Equals(expected, StringComparison.OrdinalIgnoreCase);
    }
}
