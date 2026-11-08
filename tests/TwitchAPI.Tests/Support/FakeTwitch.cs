using System.Collections.Concurrent;
using System.Net;
using System.Text;

namespace TwitchAPI.Tests.Support;

public sealed class FakeTwitch
{
    public const string TokenPath = "/oauth2/token";
    public const string StreamsPath = "/helix/streams";
    public const string UsersPath = "/helix/users";
    public const string DefaultAccessToken = "fake-app-access-token";
    public const string HelixUnauthorizedJson = """{"error":"Unauthorized","status":401,"message":"Invalid OAuth token"}""";
    public const string ExistingUserJson = """
        {
          "data": [
            {
              "id": "141981764",
              "login": "twitchdev",
              "display_name": "TwitchDev",
              "type": "",
              "broadcaster_type": "partner",
              "description": "Supporting third-party developers building Twitch integrations.",
              "profile_image_url": "https://static-cdn.jtvnw.net/profile.png",
              "offline_image_url": "",
              "view_count": 5980557,
              "email": "not-real@email.com",
              "created_at": "2016-12-14T20:32:28Z"
            }
          ]
        }
        """;

    public static readonly TimeSpan DefaultTokenLifetime = TimeSpan.FromSeconds(5000000);

    private readonly ConcurrentQueue<RecordedRequest> _requests = new();
    private readonly ConcurrentDictionary<string, ConcurrentQueue<(HttpStatusCode Status, string Json)>> _onceResponses = new();
    private readonly ConcurrentDictionary<string, Task> _holds = new();
    private readonly ConcurrentDictionary<string, (HttpStatusCode Status, string Json)> _responses = new()
    {
        [TokenPath] = (HttpStatusCode.OK, TokenJson(DefaultAccessToken)),
        [StreamsPath] = (HttpStatusCode.OK, """{"data":[],"pagination":{}}"""),
        [UsersPath] = (HttpStatusCode.OK, """{"data":[]}"""),
    };

    public static string TokenJson(string accessToken) =>
        $$"""{"access_token":"{{accessToken}}","expires_in":{{(long)DefaultTokenLifetime.TotalSeconds}},"token_type":"bearer"}""";

    public IReadOnlyList<RecordedRequest> RequestsTo(string path) =>
        [.. _requests.Where(request => request.Uri.AbsolutePath == path)];

    public void Respond(string path, HttpStatusCode status, string json) => _responses[path] = (status, json);

    public void RespondOnce(string path, HttpStatusCode status, string json) =>
        _onceResponses.GetOrAdd(path, _ => new()).Enqueue((status, json));

    public void HoldResponsesUntil(string path, Task release) => _holds[path] = release;

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
        var path = request.RequestUri!.AbsolutePath;
        _requests.Enqueue(new RecordedRequest(request.Method, request.RequestUri, headers, body));

        if (_holds.TryGetValue(path, out var release))
        {
            await release.WaitAsync(cancellationToken);
        }

        if (!TryTakeResponse(path, out var response))
        {
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        return new HttpResponseMessage(response.Status)
        {
            Content = new StringContent(response.Json, Encoding.UTF8, "application/json"),
        };
    }

    private bool TryTakeResponse(string path, out (HttpStatusCode Status, string Json) response)
    {
        if (_onceResponses.TryGetValue(path, out var queue) && queue.TryDequeue(out response))
        {
            return true;
        }

        return _responses.TryGetValue(path, out response);
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
