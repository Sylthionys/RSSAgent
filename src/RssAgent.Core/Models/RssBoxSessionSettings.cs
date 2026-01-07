namespace RssAgent.Core.Models;

public sealed class RssBoxSessionSettings
{
    public string BaseUrl { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool Headless { get; set; } = true;
}
