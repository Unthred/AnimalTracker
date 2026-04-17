using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace AnimalTracker.Services;

public sealed class LocalAnimalRecognitionClient(
    IHttpClientFactory httpClientFactory,
    IOptions<RecognitionOptions> options) : IAnimalRecognitionService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<RecognitionResponse?> RecognizeAsync(Stream imageStream, string fileName, CancellationToken cancellationToken = default)
    {
        var opt = options.Value;
        if (string.IsNullOrWhiteSpace(opt.BaseUrl))
            return null;

        await using var buffer = new MemoryStream();
        if (imageStream.CanSeek)
            imageStream.Position = 0;
        await imageStream.CopyToAsync(buffer, cancellationToken);
        buffer.Position = 0;

        var client = httpClientFactory.CreateClient(nameof(LocalAnimalRecognitionClient));
        var maxRetries = Math.Clamp(opt.MaxRetries, 0, 5);
        for (var attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                buffer.Position = 0;
                using var content = new MultipartFormDataContent();
                var streamContent = new StreamContent(buffer);
                streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                content.Add(streamContent, "image", fileName);

                using var request = new HttpRequestMessage(HttpMethod.Post, "recognize");
                request.Content = content;
                if (!string.IsNullOrWhiteSpace(opt.ApiKey))
                    request.Headers.TryAddWithoutValidation("X-Api-Key", opt.ApiKey);

                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests && attempt < maxRetries)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(200 * (attempt + 1)), cancellationToken);
                    continue;
                }

                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                if (!response.IsSuccessStatusCode)
                    return null;

                return JsonSerializer.Deserialize<RecognitionResponse>(body, JsonOptions);
            }
            catch (HttpRequestException) when (attempt < maxRetries)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(200 * (attempt + 1)), cancellationToken);
            }
            catch (TaskCanceledException) when (attempt < maxRetries)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(200 * (attempt + 1)), cancellationToken);
            }
        }

        return null;
    }
}
