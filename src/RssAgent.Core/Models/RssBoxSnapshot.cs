using System;
using System.Collections.Generic;

namespace RssAgent.Core.Models;

public sealed class RssBoxSnapshot
{
    public IReadOnlyList<FeedInfo> Feeds { get; set; } = Array.Empty<FeedInfo>();
    public IReadOnlyList<TagInfo> Tags { get; set; } = Array.Empty<TagInfo>();
    public IReadOnlyList<DigestInfo> Digests { get; set; } = Array.Empty<DigestInfo>();
    public RssBoxOptionSet Options { get; set; } = new();
}
