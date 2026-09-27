using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text.Json;
using System.Windows.Media.Imaging;
using B5MShot.App.Models;

namespace B5MShot.App.Services;

public sealed class UploadService
{
    private static readonly Uri UploadEndpoint = new("https://s.bu5inessman.ru/api/screenshots");
    private static readonly IPAddress FallbackAddress = IPAddress.Parse("194.31.223.169");
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(45) };

    public async Task<UploadResult> UploadAsync(BitmapSource image, CancellationToken cancellationToken = default)
    {
        var imageBytes = await Task.Run(() => EncodePng(image), cancellationToken);
        try
        {
            return await SendAsync(Client, UploadEndpoint, imageBytes, cancellationToken);
        }
        catch (HttpRequestException primaryException) when (!cancellationToken.IsCancellationRequested &&
            primaryException.InnerException is SocketException socketException &&
            socketException.SocketErrorCode is SocketError.HostNotFound or SocketError.TryAgain or SocketError.NoData)
        {
            using var fallbackClient = CreateFallbackClient(FallbackAddress);
            try
            {
                return await SendAsync(fallbackClient, UploadEndpoint, imageBytes, cancellationToken);
            }
            catch (Exception fallbackException)
            {
                throw new InvalidOperationException(
                    $"Домен не найден через системный DNS, а резервное подключение к VPS не удалось: {fallbackException.Message}",
                    primaryException);
            }
        }
    }

    private static byte[] EncodePng(BitmapSource image)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }

    private static async Task<UploadResult> SendAsync(HttpClient client, Uri endpoint, byte[] imageBytes, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        using var form = new MultipartFormDataContent();
        using var file = new ByteArrayContent(imageBytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(file, "file", "screenshot.png");
        request.Content = form;

        using var response = await client.SendAsync(request, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Сервер отклонил загрузку ({(int)response.StatusCode}): {json}");
        }

        return JsonSerializer.Deserialize<UploadResult>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("Сервер вернул некорректный ответ.");
    }

    private static HttpClient CreateFallbackClient(IPAddress address)
    {
        var handler = new SocketsHttpHandler
        {
            ConnectCallback = async (context, cancellationToken) =>
            {
                var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                try
                {
                    await socket.ConnectAsync(new IPEndPoint(address, context.DnsEndPoint.Port), cancellationToken);
                    return new NetworkStream(socket, ownsSocket: true);
                }
                catch
                {
                    socket.Dispose();
                    throw;
                }
            }
        };

        return new HttpClient(handler, disposeHandler: true) { Timeout = TimeSpan.FromSeconds(45) };
    }
}
