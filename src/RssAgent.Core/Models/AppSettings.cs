namespace RssAgent.Core.Models;

public sealed class AppSettings
{
    public RssBoxSettings RssBox { get; set; } = new();
    public RssHubSettings RssHub { get; set; } = new();
    public GeminiSettings Gemini { get; set; } = new();
    public DefaultOptions Defaults { get; set; } = new();
}

public sealed class RssBoxSettings
{
    public string BaseUrl { get; set; } = "http://localhost:18000";
    public string Username { get; set; } = string.Empty;
    public string PasswordEncrypted { get; set; } = string.Empty;
}

public sealed class RssHubSettings
{
    public string BaseUrl { get; set; } = "http://localhost:21200";
}

public sealed class GeminiSettings
{
    public string ApiKeyEncrypted { get; set; } = string.Empty;
    public string Model { get; set; } = "gemini-3-flash-preview";
}

public sealed class DefaultOptions
{
    public string TargetLanguage { get; set; } = "Chinese Simplified";
    public string DefaultTranslatorOption { get; set; } = string.Empty;
    public string DefaultSummarizerOption { get; set; } = string.Empty;
    public string DefaultFilterOption { get; set; } = string.Empty;
    public string DefaultTranslationDisplay { get; set; } = string.Empty;
}
