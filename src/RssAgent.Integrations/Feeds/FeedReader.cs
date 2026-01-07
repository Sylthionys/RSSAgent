using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.ServiceModel.Syndication;
using System.Text.Json;
using System.Xml;

namespace RssAgent.Integrations.Feeds;

public sealed class FeedReader
{
    public IReadOnlyList<string> ExtractSampleTexts(string content, int maxItems = 5)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return Array.Empty<string>();
        }

        var trimmed = content.TrimStart();
        if (trimmed.StartsWith("<", StringComparison.Ordinal))
        {
            return ExtractFromXml(trimmed, maxItems);
        }

        if (trimmed.StartsWith("{", StringComparison.Ordinal) || trimmed.StartsWith("[", StringComparison.Ordinal))
        {
            return ExtractFromJson(trimmed, maxItems);
        }

        return Array.Empty<string>();
    }

    private static IReadOnlyList<string> ExtractFromXml(string xml, int maxItems)
    {
        try
        {
            using var stringReader = new StringReader(xml);
            using var xmlReader = XmlReader.Create(stringReader, new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore });
            var feed = SyndicationFeed.Load(xmlReader);
            if (feed == null)
            {
                return Array.Empty<string>();
            }

            return feed.Items
                .Select(item => $"{item.Title?.Text} {item.Summary?.Text}".Trim())
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .Take(maxItems)
                .ToList();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private static IReadOnlyList<string> ExtractFromJson(string json, int maxItems)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("items", out var items))
            {
                return Array.Empty<string>();
            }

            var results = new List<string>();
            foreach (var item in items.EnumerateArray())
            {
                var title = item.TryGetProperty("title", out var titleElement) ? titleElement.GetString() : string.Empty;
                var summary = item.TryGetProperty("summary", out var summaryElement) ? summaryElement.GetString() : string.Empty;
                var text = $"{title} {summary}".Trim();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    results.Add(text);
                }

                if (results.Count >= maxItems)
                {
                    break;
                }
            }

            return results;
        }
        catch
        {
            return Array.Empty<string>();
        }
    }
}
