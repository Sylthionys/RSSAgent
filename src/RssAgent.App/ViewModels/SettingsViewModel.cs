using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using RssAgent.Automation;
using RssAgent.Core.Logging;
using RssAgent.Core.Models;
using RssAgent.Core.Services;
using RssAgent.Integrations.Gemini;
using RssAgent.Integrations.RssBox;
using RssAgent.Integrations.RssHub;

namespace RssAgent.App.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private const string DefaultSnapshotDirectory = @"S:\Documents\Project\html";
    private readonly SettingsService _settingsService;
    private readonly RssBoxClient _rssBoxClient;
    private readonly RssHubClient _rssHubClient;
    private readonly AppLogger _logger;

    public ObservableCollection<ServiceCheckResult> HealthChecks { get; } = new();

    public ObservableCollection<RssBoxOptionItem> TranslatorOptions { get; } = new();
    public ObservableCollection<RssBoxOptionItem> SummarizerOptions { get; } = new();
    public ObservableCollection<RssBoxOptionItem> FilterOptions { get; } = new();
    public ObservableCollection<RssBoxOptionItem> TranslationDisplayOptions { get; } = new();
    public ObservableCollection<RssBoxOptionItem> TargetLanguageOptions { get; } = new();

    public bool IsSnapshotDebugEnabled { get; } = 
#if DEBUG
        true
#else
        string.Equals(Environment.GetEnvironmentVariable("RSSAGENT_SNAPSHOT"), "1", StringComparison.OrdinalIgnoreCase)
#endif
    ;

    [ObservableProperty] private string _rssBoxBaseUrl = "http://localhost:18000";
    [ObservableProperty] private string _rssBoxUsername = string.Empty;
    [ObservableProperty] private string _rssBoxPassword = string.Empty;

    [ObservableProperty] private string _rssHubBaseUrl = "http://localhost:21200";

    [ObservableProperty] private string _geminiApiKey = string.Empty;
    [ObservableProperty] private string _geminiModel = "gemini-3-flash-preview";

    [ObservableProperty] private string _targetLanguage = "Chinese Simplified";
    [ObservableProperty] private string _defaultTranslatorOption = string.Empty;
    [ObservableProperty] private string _defaultSummarizerOption = string.Empty;
    [ObservableProperty] private string _defaultFilterOption = string.Empty;
    [ObservableProperty] private string _defaultTranslationDisplay = string.Empty;

    [ObservableProperty] private string _snapshotDirectory = DefaultSnapshotDirectory;
    [ObservableProperty] private string _snapshotPreview = string.Empty;

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _statusMessage = string.Empty;

    public SettingsViewModel(
        SettingsService settingsService,
        RssBoxClient rssBoxClient,
        RssHubClient rssHubClient,
        AppLogger logger)
    {
        _settingsService = settingsService;
        _rssBoxClient = rssBoxClient;
        _rssHubClient = rssHubClient;
        _logger = logger;
        SnapshotDirectory = Environment.GetEnvironmentVariable("RSSAGENT_SNAPSHOT_DIR") ?? DefaultSnapshotDirectory;
    }

    public async Task LoadAsync()
    {
        var settings = await _settingsService.LoadForUiAsync();
        Apply(settings);
    }

    public UserSettings BuildUserSettings()
    {
        return new UserSettings
        {
            RssBoxBaseUrl = RssBoxBaseUrl,
            RssBoxUsername = RssBoxUsername,
            RssBoxPassword = RssBoxPassword,
            RssHubBaseUrl = RssHubBaseUrl,
            GeminiApiKey = GeminiApiKey,
            GeminiModel = GeminiModel,
            TargetLanguage = TargetLanguage,
            DefaultTranslatorOption = DefaultTranslatorOption,
            DefaultSummarizerOption = DefaultSummarizerOption,
            DefaultFilterOption = DefaultFilterOption,
            DefaultTranslationDisplay = DefaultTranslationDisplay
        };
    }

    public void Apply(UserSettings settings)
    {
        RssBoxBaseUrl = settings.RssBoxBaseUrl;
        RssBoxUsername = settings.RssBoxUsername;
        RssBoxPassword = settings.RssBoxPassword;
        RssHubBaseUrl = settings.RssHubBaseUrl;
        GeminiApiKey = settings.GeminiApiKey;
        GeminiModel = settings.GeminiModel;
        TargetLanguage = settings.TargetLanguage;
        DefaultTranslatorOption = settings.DefaultTranslatorOption;
        DefaultSummarizerOption = settings.DefaultSummarizerOption;
        DefaultFilterOption = settings.DefaultFilterOption;
        DefaultTranslationDisplay = settings.DefaultTranslationDisplay;
    }


    public void ApplyRssBoxOptions(RssBoxOptionSet options)
    {
        UpdateOptions(TranslatorOptions, options.TranslatorOptions);
        UpdateOptions(SummarizerOptions, options.SummarizerOptions);
        UpdateOptions(FilterOptions, options.FilterOptions);
        UpdateOptions(TranslationDisplayOptions, options.TranslationDisplayOptions);
        UpdateOptions(TargetLanguageOptions, options.TargetLanguageOptions);

        if (!string.IsNullOrWhiteSpace(options.DefaultTargetLanguage))
        {
            TargetLanguage = options.DefaultTargetLanguage;
        }
        else if (TargetLanguageOptions.Count > 0 && !TargetLanguageOptions.Any(o => string.Equals(o.Value, TargetLanguage, StringComparison.OrdinalIgnoreCase)))
        {
            TargetLanguage = TargetLanguageOptions.FirstOrDefault(o => !string.IsNullOrWhiteSpace(o.Value))?.Value ?? TargetLanguage;
        }

        DefaultTranslatorOption = EnsureSelection(DefaultTranslatorOption, TranslatorOptions);
        DefaultSummarizerOption = EnsureSelection(DefaultSummarizerOption, SummarizerOptions);
        DefaultTranslationDisplay = EnsureSelection(DefaultTranslationDisplay, TranslationDisplayOptions);
        DefaultFilterOption = FilterOptions.Any(o => string.Equals(o.Value, DefaultFilterOption, StringComparison.OrdinalIgnoreCase))
            ? DefaultFilterOption
            : string.Empty;
    }

    public RssBoxOptionItem? ResolveFilterOption(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return FilterOptions.FirstOrDefault(option => string.Equals(option.Value, value, StringComparison.OrdinalIgnoreCase));
    }

    private static void UpdateOptions(ObservableCollection<RssBoxOptionItem> target, IReadOnlyList<RssBoxOptionItem> source)
    {
        target.Clear();
        foreach (var option in source)
        {
            target.Add(option);
        }
    }

    private static string EnsureSelection(string current, ObservableCollection<RssBoxOptionItem> options)
    {
        if (options.Count == 0)
        {
            return current;
        }

        if (options.Any(option => string.Equals(option.Value, current, StringComparison.OrdinalIgnoreCase)))
        {
            return current;
        }

        var first = options.FirstOrDefault(option => !string.IsNullOrWhiteSpace(option.Value)) ?? options[0];
        return first.Value;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        IsBusy = true;
        StatusMessage = "保存中...";
        try
        {
            await _settingsService.SaveFromUiAsync(BuildUserSettings());
            StatusMessage = "设置已保存。";
            await _logger.InfoAsync(StatusMessage);
        }
        catch (Exception ex)
        {
            StatusMessage = $"保存失败: {ex.Message}";
            await _logger.ErrorAsync("保存设置失败。", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ExportConfigAsync()
    {
        var settings = await _settingsService.LoadRawAsync();
        var json = _settingsService.ExportSanitizedJson(settings);

        var dialog = new SaveFileDialog
        {
            FileName = "rssagent-settings.json",
            Filter = "JSON Files (*.json)|*.json",
            OverwritePrompt = true
        };

        if (dialog.ShowDialog() == true)
        {
            await System.IO.File.WriteAllTextAsync(dialog.FileName, json);
            StatusMessage = $"已导出: {dialog.FileName}";
        }
    }

    [RelayCommand]
    private async Task TestRssBoxAsync()
    {
        IsBusy = true;
        var result = await _rssBoxClient.CheckAsync(RssBoxBaseUrl);
        UpdateCheck(result);
        IsBusy = false;
    }

    [RelayCommand]
    private async Task TestRssHubAsync()
    {
        IsBusy = true;
        var result = await _rssHubClient.CheckAsync(RssHubBaseUrl);
        UpdateCheck(result);
        IsBusy = false;
    }

    [RelayCommand]
    private void PreviewSnapshot()
    {
        if (!IsSnapshotDebugEnabled)
        {
            SnapshotPreview = "Snapshot preview is disabled.";
            return;
        }

        IsBusy = true;
        try
        {
            var parser = new RssBoxSnapshotParser();
            var snapshot = parser.Parse(SnapshotDirectory);
            ApplyRssBoxOptions(snapshot.Options);
            SnapshotPreview = $"Feeds: {snapshot.Feeds.Count}, Tags: {snapshot.Tags.Count}, Digests: {snapshot.Digests.Count}, Filters: {snapshot.Options.FilterOptions.Count}";
        }
        catch (Exception ex)
        {
            SnapshotPreview = $"Snapshot parse failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task TestGeminiAsync()
    {
        IsBusy = true;
        if (string.IsNullOrWhiteSpace(GeminiApiKey))
        {
            UpdateCheck(new ServiceCheckResult
            {
                ServiceName = "Gemini",
                Success = false,
                Detail = "Missing API key"
            });
            IsBusy = false;
            return;
        }

        var gemini = new GeminiClient(GeminiApiKey, GeminiModel, _logger);
        var (_, result) = await gemini.GeneratePlanAsync("订阅 Hacker News", "single");
        var check = new ServiceCheckResult
        {
            ServiceName = "Gemini",
            Success = result.Success,
            Detail = result.Success ? "OK" : $"{result.Error ?? "未知错误"} {result.Detail}".Trim()
        };
        UpdateCheck(check);
        IsBusy = false;
    }

    public async Task RunStartupChecksAsync()
    {
        var rssbox = _rssBoxClient.CheckAsync(RssBoxBaseUrl);
        var rsshb = _rssHubClient.CheckAsync(RssHubBaseUrl);
        var results = await Task.WhenAll(rssbox, rsshb);
        foreach (var result in results)
        {
            UpdateCheck(result);
        }

    }

    private void UpdateCheck(ServiceCheckResult result)
    {
        for (var i = 0; i < HealthChecks.Count; i++)
        {
            if (HealthChecks[i].ServiceName == result.ServiceName)
            {
                HealthChecks[i] = result;
                StatusMessage = $"{result.ServiceName}: {result.Detail}";
                return;
            }
        }

        HealthChecks.Add(result);
        StatusMessage = $"{result.ServiceName}: {result.Detail}";
    }
}
