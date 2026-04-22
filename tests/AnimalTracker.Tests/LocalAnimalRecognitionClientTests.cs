using System.Net;
using System.Net.Http.Headers;
using System.Text;
using AnimalTracker.Services;
using Microsoft.Extensions.Options;

namespace AnimalTracker.Tests;

public sealed class LocalAnimalRecognitionClientTests
{
    [Fact]
    public async Task RecognizeAsync_returns_null_when_base_url_missing()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        var client = new LocalAnimalRecognitionClient(
            new TestHttpClientFactory(new HttpClient(handler)),
            Options.Create(new RecognitionOptions { BaseUrl = "" }));

        await using var ms = new MemoryStream(Encoding.UTF8.GetBytes("x"));
        var result = await client.RecognizeAsync(ms, "photo.jpg");

        Assert.Null(result);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task RecognizeAsync_sends_api_key_and_deserializes_success_body()
    {
        HttpRequestMessage? captured = null;
        var handler = new StubHttpMessageHandler((request, _) =>
        {
            captured = request;
            var json = """
                       {
                         "modelVersion":"v1",
                         "processingMs":12,
                         "detections":[{"topCandidates":[{"label":"Fox","confidence":0.91}]}],
                         "imageLevelCandidates":[]
                       }
                       """;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        var client = new LocalAnimalRecognitionClient(
            new TestHttpClientFactory(httpClient),
            Options.Create(new RecognitionOptions { BaseUrl = "http://localhost", ApiKey = "abc123" }));

        await using var ms = new MemoryStream([1, 2, 3, 4]);
        var result = await client.RecognizeAsync(ms, "photo.jpg");

        Assert.NotNull(result);
        Assert.Equal("v1", result!.ModelVersion);
        Assert.Single(result.Detections);
        Assert.Equal("Fox", result.Detections[0].TopCandidates[0].Label);
        Assert.NotNull(captured);
        Assert.True(captured!.Headers.TryGetValues("X-Api-Key", out var values));
        Assert.Equal("abc123", values.Single());
        Assert.Equal(HttpMethod.Post, captured.Method);
        Assert.EndsWith("/recognize", captured.RequestUri!.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RecognizeAsync_retries_on_429_then_succeeds()
    {
        var call = 0;
        var handler = new StubHttpMessageHandler((_, _) =>
        {
            call++;
            if (call == 1)
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests));

            var json = """
                       {
                         "processingMs":5,
                         "detections":[],
                         "imageLevelCandidates":[{"label":"Badger","confidence":0.5}]
                       }
                       """;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        var client = new LocalAnimalRecognitionClient(
            new TestHttpClientFactory(httpClient),
            Options.Create(new RecognitionOptions { BaseUrl = "http://localhost", MaxRetries = 2 }));

        await using var ms = new MemoryStream([9, 8, 7]);
        var result = await client.RecognizeAsync(ms, "photo.jpg");

        Assert.NotNull(result);
        Assert.Equal(2, handler.CallCount);
        Assert.Equal("Badger", result!.ImageLevelCandidates[0].Label);
    }

    [Fact]
    public async Task RecognizeAsync_returns_null_on_non_success_status()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("boom", Encoding.UTF8, "text/plain")
            }));
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        var client = new LocalAnimalRecognitionClient(
            new TestHttpClientFactory(httpClient),
            Options.Create(new RecognitionOptions { BaseUrl = "http://localhost" }));

        await using var ms = new MemoryStream([1, 2, 3]);
        var result = await client.RecognizeAsync(ms, "photo.jpg");

        Assert.Null(result);
        Assert.Equal(1, handler.CallCount);
    }

    private sealed class TestHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder) : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            return await responder(request, cancellationToken);
        }
    }
}
