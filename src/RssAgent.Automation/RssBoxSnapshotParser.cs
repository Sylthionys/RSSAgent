using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using RssAgent.Core.Models;

namespace RssAgent.Automation;

public sealed class RssBoxSnapshotParser
{
    private static readonly Regex TagIdRegex = new(@"/core/tag/(?<id>\\d+)/change", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex FilterIdRegex = new(@"/core/filter/(?<id>\\d+)/change", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public RssBoxSnapshot Parse(string snapshotDir)
    {
        var snapshot = new RssBoxSnapshot();

        var feedsDoc = LoadDocument(Path.Combine(snapshotDir, "选择 源 来修改 _ RSSBox.html"));
        var tagsDoc = LoadDocument(Path.Combine(snapshotDir, "选择 tag 来修改 _ RSSBox.html"));
        var digestsDoc = LoadDocument(Path.Combine(snapshotDir, "选择 简报 来修改 _ RSSBox.html"));
        var addFeedDoc = LoadDocument(Path.Combine(snapshotDir, "增加 源 _ RSSBox.html"));
        var filtersDoc = LoadDocument(Path.Combine(snapshotDir, "选择 过滤器 来修改 _ RSSBox.html"));

        snapshot.Feeds = ParseFeeds(feedsDoc);
        snapshot.Tags = ParseTags(tagsDoc);
        snapshot.Digests = ParseDigests(digestsDoc);
        snapshot.Options = ParseOptions(addFeedDoc, filtersDoc);

        return snapshot;
    }

    private static HtmlDocument? LoadDocument(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        var html = File.ReadAllText(path);
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        return doc;
    }

    private static IReadOnlyList<FeedInfo> ParseFeeds(HtmlDocument? doc)
    {
        if (doc == null)
        {
            return Array.Empty<FeedInfo>();
        }

        var rows = doc.DocumentNode.SelectNodes("//table[@id='result_list']//tbody//tr");
        if (rows == null)
        {
            return Array.Empty<FeedInfo>();
        }

        var feeds = new List<FeedInfo>();
        foreach (var row in rows)
        {
            var titleNode = row.SelectSingleNode(".//th[contains(@class,'field-name')]//a");
            var title = HtmlEntity.DeEntitize(titleNode?.InnerText ?? string.Empty).Trim();

            var urlNode = row.SelectSingleNode(".//td[contains(@class,'field-fetch_feed')]//a[starts-with(@href,'http')]");
            var url = urlNode?.GetAttributeValue("href", string.Empty) ?? string.Empty;

            var tagsNode = row.SelectSingleNode(".//td[contains(@class,'field-show_tags')]");
            var tagsText = HtmlEntity.DeEntitize(tagsNode?.InnerText ?? string.Empty).Trim();
            var tags = tagsText.Split(new[] { ',', '，', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

            var statusNode = row.SelectSingleNode(".//td[contains(@class,'field-fetch_info')]//span");
            var status = HtmlEntity.DeEntitize(statusNode?.InnerText ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(url))
            {
                continue;
            }

            feeds.Add(new FeedInfo
            {
                Title = title,
                Url = url,
                Tags = tags,
                Status = status
            });
        }

        return feeds;
    }

    private static IReadOnlyList<TagInfo> ParseTags(HtmlDocument? doc)
    {
        if (doc == null)
        {
            return Array.Empty<TagInfo>();
        }

        var rows = doc.DocumentNode.SelectNodes("//table[@id='result_list']//tbody//tr");
        if (rows == null)
        {
            return Array.Empty<TagInfo>();
        }

        var tags = new List<TagInfo>();
        foreach (var row in rows)
        {
            var link = row.SelectSingleNode(".//th[contains(@class,'field-name')]//a");
            var name = HtmlEntity.DeEntitize(link?.InnerText ?? string.Empty).Trim();
            var href = link?.GetAttributeValue("href", string.Empty) ?? string.Empty;
            var idMatch = TagIdRegex.Match(href);
            var id = idMatch.Success ? idMatch.Groups["id"].Value : null;

            if (!string.IsNullOrWhiteSpace(name))
            {
                tags.Add(new TagInfo
                {
                    Id = id,
                    Name = name
                });
            }
        }

        return tags;
    }

    private static IReadOnlyList<DigestInfo> ParseDigests(HtmlDocument? doc)
    {
        if (doc == null)
        {
            return Array.Empty<DigestInfo>();
        }

        var rows = doc.DocumentNode.SelectNodes("//table[@id='result_list']//tbody//tr");
        if (rows == null)
        {
            return Array.Empty<DigestInfo>();
        }

        var digests = new List<DigestInfo>();
        foreach (var row in rows)
        {
            var nameNode = row.SelectSingleNode(".//th[contains(@class,'field-name')]//a");
            var name = HtmlEntity.DeEntitize(nameNode?.InnerText ?? string.Empty).Trim();

            var cells = row.SelectNodes("td");
            var schedule = cells != null && cells.Count > 0
                ? HtmlEntity.DeEntitize(cells[0].InnerText).Trim()
                : string.Empty;

            if (!string.IsNullOrWhiteSpace(name))
            {
                digests.Add(new DigestInfo
                {
                    Name = name,
                    Schedule = schedule
                });
            }
        }

        return digests;
    }

    private static RssBoxOptionSet ParseOptions(HtmlDocument? addFeedDoc, HtmlDocument? filtersDoc)
    {
        var optionSet = new RssBoxOptionSet();

        if (addFeedDoc != null)
        {
            optionSet.TranslatorOptions = ParseSelectOptions(addFeedDoc, "id_translator_option");
            optionSet.SummarizerOptions = ParseSelectOptions(addFeedDoc, "id_summarizer");
            optionSet.TranslationDisplayOptions = ParseSelectOptions(addFeedDoc, "id_translation_display");
            optionSet.TargetLanguageOptions = ParseSelectOptions(addFeedDoc, "id_target_language");
            optionSet.DefaultTargetLanguage = ParseSelectedOption(addFeedDoc, "id_target_language");
        }

        optionSet.FilterOptions = ParseFilters(filtersDoc);
        return optionSet;
    }

    private static IReadOnlyList<RssBoxOptionItem> ParseSelectOptions(HtmlDocument doc, string selectId)
    {
        var select = doc.DocumentNode.SelectSingleNode($"//select[@id='{selectId}']");
        if (select == null)
        {
            return Array.Empty<RssBoxOptionItem>();
        }

        var options = new List<RssBoxOptionItem>();
        var nodes = select.SelectNodes(".//option") ?? Enumerable.Empty<HtmlNode>();
        foreach (var node in nodes)
        {
            var value = node.GetAttributeValue("value", string.Empty);
            var label = HtmlEntity.DeEntitize(node.InnerText ?? string.Empty).Trim();
            options.Add(new RssBoxOptionItem(value, label));
        }

        return options;
    }

    private static string? ParseSelectedOption(HtmlDocument doc, string selectId)
    {
        var select = doc.DocumentNode.SelectSingleNode($"//select[@id='{selectId}']");
        var selected = select?.SelectSingleNode(".//option[@selected]");
        var value = selected?.GetAttributeValue("value", string.Empty);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static IReadOnlyList<RssBoxOptionItem> ParseFilters(HtmlDocument? doc)
    {
        if (doc == null)
        {
            return Array.Empty<RssBoxOptionItem>();
        }

        var rows = doc.DocumentNode.SelectNodes("//table[@id='result_list']//tbody//tr");
        if (rows == null)
        {
            return Array.Empty<RssBoxOptionItem>();
        }

        var filters = new List<RssBoxOptionItem>();
        foreach (var row in rows)
        {
            var link = row.SelectSingleNode(".//th[contains(@class,'field-name')]//a");
            var name = HtmlEntity.DeEntitize(link?.InnerText ?? string.Empty).Trim();
            var href = link?.GetAttributeValue("href", string.Empty) ?? string.Empty;
            var match = FilterIdRegex.Match(href);
            var id = match.Success ? match.Groups["id"].Value : null;

            if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(id))
            {
                filters.Add(new RssBoxOptionItem(id, name));
            }
        }

        return filters;
    }
}
