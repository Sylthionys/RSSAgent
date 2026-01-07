using System;
using System.Collections.Generic;

namespace RssAgent.Core.Models;

public sealed class RssBoxOptionItem
{
    public RssBoxOptionItem()
    {
    }

    public RssBoxOptionItem(string value, string label)
    {
        Value = value;
        Label = label;
    }

    public string Value { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}

public sealed class RssBoxOptionSet
{
    public IReadOnlyList<RssBoxOptionItem> TranslatorOptions { get; set; } = Array.Empty<RssBoxOptionItem>();
    public IReadOnlyList<RssBoxOptionItem> SummarizerOptions { get; set; } = Array.Empty<RssBoxOptionItem>();
    public IReadOnlyList<RssBoxOptionItem> FilterOptions { get; set; } = Array.Empty<RssBoxOptionItem>();
    public IReadOnlyList<RssBoxOptionItem> TranslationDisplayOptions { get; set; } = Array.Empty<RssBoxOptionItem>();
    public IReadOnlyList<RssBoxOptionItem> TargetLanguageOptions { get; set; } = Array.Empty<RssBoxOptionItem>();
    public string? DefaultTargetLanguage { get; set; }
}
