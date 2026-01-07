using System.Collections.Generic;

namespace RssAgent.Core.Models;

public sealed class FeedValidationResult
{
    public bool Success { get; set; }
    public int StatusCode { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public IReadOnlyList<string> SampleTexts { get; set; } = new List<string>();
    public string Error { get; set; } = string.Empty;
}
