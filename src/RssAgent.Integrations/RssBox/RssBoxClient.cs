using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using RssAgent.Automation;
using RssAgent.Core;
using RssAgent.Core.Logging;
using RssAgent.Core.Models;
using RssAgent.Integrations.Http;

namespace RssAgent.Integrations.RssBox;

public sealed class RssBoxClient
{
    private readonly ResilientHttpClient _http;
    private readonly RssBoxAutomation _automation;
    private readonly AppLogger _logger;

    public RssBoxClient(AppLogger logger)
    {
        _logger = logger;
        _automation = new RssBoxAutomation(logger);
        _http = new ResilientHttpClient(new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15)
        });
    }

    public async Task<ServiceCheckResult> CheckAsync(string baseUrl, CancellationToken cancellationToken = default)
    {
        var result = new ServiceCheckResult { ServiceName = "RSSBox" };
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            result.Success = false;
            result.Detail = "BaseUrl 为空。";
            return result;
        }

        try
        {
            var response = await _http.SendAsync(new HttpRequestMessage(HttpMethod.Get, baseUrl), cancellationToken);
            result.Success = response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.Found;
            result.Detail = result.Success ? "OK" : $"Status {(int)response.StatusCode}";
            return result;
        }
        catch (Exception ex)
        {
            await _logger.WarnAsync("RSSBox 请求失败。", ex.Message);
            result.Success = false;
            result.Detail = ex.Message;
            return result;
        }
    }

    public Task<IReadOnlyList<FeedInfo>> GetFeedsAsync(RssBoxSessionSettings session, CancellationToken cancellationToken = default)
        => _automation.GetFeedsAsync(session, cancellationToken);

    public Task<IReadOnlyList<TagInfo>> GetTagsAsync(RssBoxSessionSettings session, CancellationToken cancellationToken = default)
        => _automation.GetTagsAsync(session, cancellationToken);

    public Task<IReadOnlyList<DigestInfo>> GetDigestsAsync(RssBoxSessionSettings session, CancellationToken cancellationToken = default)
        => _automation.GetDigestsAsync(session, cancellationToken);

    public Task<RssBoxOptionSet> GetOptionsAsync(RssBoxSessionSettings session, CancellationToken cancellationToken = default)
        => _automation.GetOptionsAsync(session, cancellationToken);

    public Task<RssBoxSnapshot> GetSnapshotAsync(RssBoxSessionSettings session, CancellationToken cancellationToken = default)
        => _automation.GetSnapshotAsync(session, cancellationToken);

    public Task<OperationResult> AddSourceAsync(RssBoxSessionSettings session, AddSourceRequest request, CancellationToken cancellationToken = default)
        => _automation.AddSourceAsync(session, request, cancellationToken);
}
