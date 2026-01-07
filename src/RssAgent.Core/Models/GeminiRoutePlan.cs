using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace RssAgent.Core.Models;

public sealed class GeminiRoutePlan
{
    [JsonPropertyName("mode")]
    public string Mode { get; set; } = "single";

    [JsonPropertyName("items")]
    public List<GeminiRouteItem> Items { get; set; } = new();
}

public sealed class GeminiRouteItem
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("rsshub")]
    public GeminiRssHubRoute? RssHub { get; set; }

    [JsonPropertyName("directRssUrl")]
    public string? DirectRssUrl { get; set; }

    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = new();

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }

    [JsonPropertyName("expectedSourceLang")]
    public string? ExpectedSourceLang { get; set; }
}

public sealed class GeminiRssHubRoute
{
    [JsonPropertyName("route")]
    public string Route { get; set; } = string.Empty;

    [JsonPropertyName("query")]
    public Dictionary<string, string> Query { get; set; } = new();
}
