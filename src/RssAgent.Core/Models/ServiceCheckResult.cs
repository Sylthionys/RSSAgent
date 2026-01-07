namespace RssAgent.Core.Models;

public sealed class ServiceCheckResult
{
    public string ServiceName { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string Detail { get; set; } = string.Empty;
}
