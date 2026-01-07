using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

namespace RssAgent.Core.Services;

public static class RssHubUrlBuilder
{
    public static string Build(string baseUrl, string route, IDictionary<string, string>? query)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new ArgumentException("BaseUrl is required.", nameof(baseUrl));
        }

        if (string.IsNullOrWhiteSpace(route))
        {
            throw new ArgumentException("Route is required.", nameof(route));
        }

        var normalizedBase = baseUrl.TrimEnd('/');
        var normalizedRoute = route.StartsWith("/", StringComparison.Ordinal) ? route : "/" + route;
        var routePath = normalizedRoute;
        var existingQuery = string.Empty;

        var queryIndex = normalizedRoute.IndexOf("?", StringComparison.Ordinal);
        if (queryIndex >= 0)
        {
            routePath = normalizedRoute[..queryIndex];
            existingQuery = normalizedRoute[(queryIndex + 1)..];
        }

        var builder = new StringBuilder($"{normalizedBase}{routePath}");

        var mergedQuery = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(existingQuery))
        {
            foreach (var pair in existingQuery.Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var parts = pair.Split('=', 2);
                if (parts.Length == 2)
                {
                    mergedQuery[WebUtility.UrlDecode(parts[0])] = WebUtility.UrlDecode(parts[1]);
                }
                else if (parts.Length == 1)
                {
                    mergedQuery[WebUtility.UrlDecode(parts[0])] = string.Empty;
                }
            }
        }
        if (query != null)
        {
            foreach (var item in query)
            {
                mergedQuery[item.Key] = item.Value ?? string.Empty;
            }
        }

        if (mergedQuery.Count > 0)
        {
            builder.Append("?");
            builder.Append(string.Join("&", mergedQuery.Select(kv =>
                $"{WebUtility.UrlEncode(kv.Key)}={WebUtility.UrlEncode(kv.Value)}")));
        }

        return builder.ToString();
    }
}
