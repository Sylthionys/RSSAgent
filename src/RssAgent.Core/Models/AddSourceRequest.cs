using System.Collections.Generic;

namespace RssAgent.Core.Models;

public sealed class AddSourceRequest
{
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public int? RefreshMinutes { get; set; }
    public string TargetLanguage { get; set; } = string.Empty;
    public bool TranslateTitle { get; set; }
    public bool TranslateContent { get; set; }
    public bool GenerateSummary { get; set; }
    public string TranslatorOption { get; set; } = string.Empty;
    public string SummarizerOption { get; set; } = string.Empty;
    public string TranslationDisplay { get; set; } = string.Empty;
    public IReadOnlyList<RssBoxOptionItem> Tags { get; set; } = new List<RssBoxOptionItem>();
    public IReadOnlyList<RssBoxOptionItem> Filters { get; set; } = new List<RssBoxOptionItem>();
}
