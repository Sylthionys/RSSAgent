namespace RssAgent.Core.Models;

public sealed class UserSettings
{
    public string RssBoxBaseUrl { get; set; } = "http://localhost:18000";
    public string RssBoxUsername { get; set; } = string.Empty;
    public string RssBoxPassword { get; set; } = string.Empty;

    public string RssHubBaseUrl { get; set; } = "http://localhost:21200";

    public string GeminiApiKey { get; set; } = string.Empty;
    public string GeminiModel { get; set; } = "gemini-3-flash-preview";

    public string TargetLanguage { get; set; } = "Chinese Simplified";
    public string DefaultTranslatorOption { get; set; } = string.Empty;
    public string DefaultSummarizerOption { get; set; } = string.Empty;
    public string DefaultFilterOption { get; set; } = string.Empty;
    public string DefaultTranslationDisplay { get; set; } = string.Empty;
}
