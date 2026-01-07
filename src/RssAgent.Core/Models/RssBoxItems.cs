using System;
using System.Collections.Generic;

namespace RssAgent.Core.Models;

public sealed class FeedInfo
{
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public IReadOnlyList<string> Tags { get; set; } = Array.Empty<string>();
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset? UpdatedAt { get; set; }
}

public sealed class TagInfo
{
    public string? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int? Count { get; set; }
}

public sealed class DigestInfo
{
    public string Name { get; set; } = string.Empty;
    public string Schedule { get; set; } = string.Empty;
    public IReadOnlyList<string> Tags { get; set; } = Array.Empty<string>();
}
