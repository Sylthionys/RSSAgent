using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using RssAgent.Core;
using RssAgent.Core.Logging;
using RssAgent.Core.Models;
using RssAgent.Core.Services;
using RssAgent.Integrations.Feeds;
using RssAgent.Integrations.Http;

namespace RssAgent.Integrations.RssHub;

public sealed class RssHubClient
{
    private readonly ResilientHttpClient _http;
    private readonly FeedReader _feedReader;
    private readonly AppLogger _logger;

    public RssHubClient(AppLogger logger)
    {
        _logger = logger;
        _feedReader = new FeedReader();
        _http = new ResilientHttpClient(new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(20)
        });
    }

    public string BuildUrl(string baseUrl, string route, System.Collections.Generic.IDictionary<string, string>? query)
    {
        return RssHubUrlBuilder.Build(baseUrl, route, query);
    }

    public async Task<ServiceCheckResult> CheckAsync(string baseUrl, CancellationToken cancellationToken = default)
    {
        var result = new ServiceCheckResult { ServiceName = "RSSHub" };
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            result.Success = false;
            result.Detail = "BaseUrl 为空。";
            return result;
        }

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(new HttpRequestMessage(HttpMethod.Get, baseUrl), cancellationToken);
        }
        catch (Exception ex)
        {
            await _logger.WarnAsync("RSSHub 请求失败。", ex.Message);
            result.Success = false;
            result.Detail = ex.Message;
            return result;
        }

        result.Success = response.IsSuccessStatusCode;
        result.Detail = result.Success ? "OK" : $"Status {(int)response.StatusCode}";
        return result;
    }

    public async Task<FeedValidationResult> ValidateFeedAsync(string feedUrl, CancellationToken cancellationToken = default)
    {
        var result = new FeedValidationResult();
        if (string.IsNullOrWhiteSpace(feedUrl))
        {
            result.Error = "Feed URL 为空。";
            return result;
        }

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(new HttpRequestMessage(HttpMethod.Get, feedUrl), cancellationToken);
        }
        catch (Exception ex)
        {
            await _logger.WarnAsync("RSSHub 请求失败。", ex.Message);
            result.Error = ex.Message;
            return result;
        }

        result.StatusCode = (int)response.StatusCode;
        result.ContentType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            result.Error = content;
            return result;
        }

        var trimmed = content.TrimStart();
        if (result.ContentType.Contains("html", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("<!DOCTYPE html", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("<html", StringComparison.OrdinalIgnoreCase))
        {
            result.Error = "Received HTML instead of RSS/Atom feed.";
            return result;
        }

        var samples = _feedReader.ExtractSampleTexts(content);
        if (samples.Count == 0)
        {
            result.Error = "内容不是有效 RSS/Atom/JSON Feed。";
            return result;
        }

        result.Success = true;
        result.SampleTexts = samples;
        return result;
    }
}
