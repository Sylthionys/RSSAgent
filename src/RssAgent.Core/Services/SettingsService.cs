using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using RssAgent.Core.Models;
using RssAgent.Core.Security;

namespace RssAgent.Core.Services;

[SupportedOSPlatform("windows")]
public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly DpapiProtector _protector;

    public SettingsService(DpapiProtector protector)
    {
        _protector = protector;
    }

    public async Task<UserSettings> LoadForUiAsync()
    {
        var settings = await LoadRawAsync();
        return new UserSettings
        {
            RssBoxBaseUrl = settings.RssBox.BaseUrl,
            RssBoxUsername = settings.RssBox.Username,
            RssBoxPassword = _protector.Unprotect(settings.RssBox.PasswordEncrypted),
            RssHubBaseUrl = settings.RssHub.BaseUrl,
            GeminiApiKey = _protector.Unprotect(settings.Gemini.ApiKeyEncrypted),
            GeminiModel = settings.Gemini.Model,
            TargetLanguage = settings.Defaults.TargetLanguage,
            DefaultTranslatorOption = settings.Defaults.DefaultTranslatorOption,
            DefaultSummarizerOption = settings.Defaults.DefaultSummarizerOption,
            DefaultFilterOption = settings.Defaults.DefaultFilterOption,
            DefaultTranslationDisplay = settings.Defaults.DefaultTranslationDisplay
        };
    }

    public async Task SaveFromUiAsync(UserSettings userSettings)
    {
        var settings = new AppSettings
        {
            RssBox = new RssBoxSettings
            {
                BaseUrl = userSettings.RssBoxBaseUrl,
                Username = userSettings.RssBoxUsername,
                PasswordEncrypted = _protector.Protect(userSettings.RssBoxPassword)
            },
            RssHub = new RssHubSettings
            {
                BaseUrl = userSettings.RssHubBaseUrl
            },
            Gemini = new GeminiSettings
            {
                ApiKeyEncrypted = _protector.Protect(userSettings.GeminiApiKey),
                Model = userSettings.GeminiModel
            },
            Defaults = new DefaultOptions
            {
                TargetLanguage = userSettings.TargetLanguage,
                DefaultTranslatorOption = userSettings.DefaultTranslatorOption,
                DefaultSummarizerOption = userSettings.DefaultSummarizerOption,
                DefaultFilterOption = userSettings.DefaultFilterOption,
                DefaultTranslationDisplay = userSettings.DefaultTranslationDisplay
            }
        };

        await SaveRawAsync(settings);
    }

    public async Task<AppSettings> LoadRawAsync()
    {
        if (!File.Exists(AppPaths.SettingsFilePath))
        {
            return new AppSettings();
        }

        var json = await File.ReadAllTextAsync(AppPaths.SettingsFilePath);
        return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
    }

    public async Task SaveRawAsync(AppSettings settings)
    {
        Directory.CreateDirectory(AppPaths.AppDataRoot);
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        await File.WriteAllTextAsync(AppPaths.SettingsFilePath, json);
    }

    public string ExportSanitizedJson(AppSettings settings)
    {
        var sanitized = new AppSettings
        {
            RssBox = new RssBoxSettings
            {
                BaseUrl = settings.RssBox.BaseUrl,
                Username = settings.RssBox.Username,
                PasswordEncrypted = string.Empty
            },
            RssHub = new RssHubSettings
            {
                BaseUrl = settings.RssHub.BaseUrl
            },
            Gemini = new GeminiSettings
            {
                ApiKeyEncrypted = string.Empty,
                Model = settings.Gemini.Model
            },
            Defaults = new DefaultOptions
            {
                TargetLanguage = settings.Defaults.TargetLanguage,
                DefaultTranslatorOption = settings.Defaults.DefaultTranslatorOption,
                DefaultSummarizerOption = settings.Defaults.DefaultSummarizerOption,
                DefaultFilterOption = settings.Defaults.DefaultFilterOption,
                DefaultTranslationDisplay = settings.Defaults.DefaultTranslationDisplay
            }
        };

        return JsonSerializer.Serialize(sanitized, JsonOptions);
    }
}
