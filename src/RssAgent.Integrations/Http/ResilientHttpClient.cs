using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Polly;

namespace RssAgent.Integrations.Http;

public sealed class ResilientHttpClient
{
    private readonly HttpClient _client;
    private readonly IAsyncPolicy<HttpResponseMessage> _policy;

    public ResilientHttpClient(HttpClient client)
    {
        _client = client;
        _policy = Policy.WrapAsync(
            Policy<HttpResponseMessage>
                .Handle<HttpRequestException>()
                .OrResult(response => (int)response.StatusCode >= 500)
                .WaitAndRetryAsync(2, attempt => TimeSpan.FromMilliseconds(200 * attempt)),
            Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromSeconds(15)));
    }

    public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
    {
        return _policy.ExecuteAsync(ct => _client.SendAsync(request, ct), cancellationToken);
    }
}
