using System.Collections.Concurrent;
using System.Net;
using System.Text;

namespace TwitchAPI.Tests.Support;

public sealed class FakeTwitch
{
    public const string TokenPath = "/oauth2/token";
    public const string StreamsPath = "/helix/streams";
    public const string DefaultAccessToken = "fake-app-access-token";

    private readonly ConcurrentQueue<RecordedRequest> _requests = new();
    private readonly ConcurrentDictionary<string, (HttpStatusCode Status, string Json)> _responses = new()
    {
        [TokenPath] = (HttpStatusCode.OK, $$"""{"access_token":"{{DefaultAccessToken}}","expires_in":5000000,"token_type":"bearer"}"""),
        [StreamsPath] = (HttpStatusCode.OK, """{"data":[],"pagination":{}}"""),
    };

    public IReadOnlyList<RecordedRequest> RequestsTo(string path) =>
        [.. _requests.Where(request => request.Uri.AbsolutePath == path)];

    public void Respond(string path, HttpStatusCode status, string json) => _responses[path] = (status, json);

    public HttpMessageHandler CreateHandler() => new Handler(this);

    private async Task<HttpResponseMessage> HandleAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken);
        var headers = request.Headers.ToDictionary(
            header => header.Key,
            header => string.Join(",", header.Value),
            StringComparer.OrdinalIgnoreCase);
        _requests.Enqueue(new RecordedRequest(request.Method, request.RequestUri!, headers, body));

        if (!_responses.TryGetValue(request.RequestUri!.AbsolutePath, out var response))
        {
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        return new HttpResponseMessage(response.Status)
        {
            Content = new StringContent(response.Json, Encoding.UTF8, "application/json"),
        };
    }

    private sealed class Handler(FakeTwitch twitch) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => twitch.HandleAsync(request, cancellationToken);
    }
}

public sealed record RecordedRequest(
    HttpMethod Method,
    Uri Uri,
    IReadOnlyDictionary<string, string> Headers,
    string? Body);
