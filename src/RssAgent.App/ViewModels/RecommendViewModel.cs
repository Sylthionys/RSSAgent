using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RssAgent.Core;
using RssAgent.Core.Logging;
using RssAgent.Core.Models;
using RssAgent.Integrations.Gemini;
using RssAgent.Integrations.LanguageDetection;
using RssAgent.Integrations.RssBox;
using RssAgent.Integrations.RssHub;

namespace RssAgent.App.ViewModels;

public partial class RecommendViewModel : ObservableObject
{
    private readonly SettingsViewModel _settingsViewModel;
    private readonly RssHubClient _rssHubClient;
    private readonly RssBoxClient _rssBoxClient;
    private readonly AppLogger _logger;

    public ObservableCollection<RecommendationItemViewModel> Recommendations { get; } = new();

    [ObservableProperty] private string _topic = string.Empty;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isBusy;

    public RecommendViewModel(
        SettingsViewModel settingsViewModel,
        RssHubClient rssHubClient,
        RssBoxClient rssBoxClient,
        AppLogger logger)
    {
        _settingsViewModel = settingsViewModel;
        _rssHubClient = rssHubClient;
        _rssBoxClient = rssBoxClient;
        _logger = logger;
    }

    [RelayCommand]
    private async Task RecommendAsync()
    {
        if (string.IsNullOrWhiteSpace(Topic))
        {
            StatusMessage = "请输入主题。";
            return;
        }

        IsBusy = true;
        StatusMessage = "请求 Gemini 推荐...";

        var settings = _settingsViewModel.BuildUserSettings();
        var gemini = new GeminiClient(settings.GeminiApiKey, settings.GeminiModel, _logger);

        try
        {
            var (plan, result) = await gemini.GeneratePlanAsync(Topic, "recommend");
            if (!result.Success || plan == null)
            {
                StatusMessage = result.Error ?? "Gemini 推荐失败。";
                return;
            }

            Recommendations.Clear();
            foreach (var item in plan.Items)
            {
                Recommendations.Add(new RecommendationItemViewModel(item));
            }

            StatusMessage = $"已生成 {Recommendations.Count} 个候选。";
        }
        catch (Exception ex)
        {
            StatusMessage = $"推荐失败: {ex.Message}";
            await _logger.ErrorAsync("推荐失败。", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task AddSelectedAsync()
    {
        var selected = Recommendations.Where(r => r.IsSelected).ToList();
        if (selected.Count == 0)
        {
            StatusMessage = "请先选择要添加的条目。";
            return;
        }

        IsBusy = true;
        var settings = _settingsViewModel.BuildUserSettings();
        var gemini = new GeminiClient(settings.GeminiApiKey, settings.GeminiModel, _logger);
        var detector = new CompositeLanguageDetector(new HeuristicLanguageDetector(), gemini);

        string? lastError = null;
        foreach (var candidate in selected)
        {
            StatusMessage = $"正在添加: {candidate.Title}";
            var addResult = await AddFromPlanItemAsync(settings, candidate.Item, detector);
            if (!addResult.Success)
            {
                lastError = BuildAutomationError($"Add failed: {candidate.Title}", addResult);
                StatusMessage = lastError;
                break;
            }
        }

        StatusMessage = lastError ?? "批量添加完成。";
        IsBusy = false;
    }


    private async Task<OperationResult> AddFromPlanItemAsync(UserSettings settings, GeminiRouteItem item, CompositeLanguageDetector detector)
    {
        var feedUrl = item.DirectRssUrl ?? _rssHubClient.BuildUrl(
            settings.RssHubBaseUrl,
            item.RssHub?.Route ?? string.Empty,
            item.RssHub?.Query);

        var urlSource = item.DirectRssUrl != null ? "Direct" : "RSSHub";
        if (!TryValidateGeneratedFeedUrl(feedUrl, out var validationError))
        {
            await _logger.ErrorAsync("GeneratedFeedUrl invalid.", $"{validationError} | Source: {urlSource} | Url: {feedUrl}");
            return OperationResult.Fail("Generated feed URL is invalid/empty.");
        }

        await _logger.InfoAsync("GeneratedFeedUrl", $"{feedUrl} | Source: {urlSource}");
        var validation = await _rssHubClient.ValidateFeedAsync(feedUrl);
        if (!validation.Success)
        {
            return OperationResult.Fail("RSSHub 验证失败。", validation.Error);
        }

        var sampleText = string.Join(" ", validation.SampleTexts);
        var sourceLang = item.ExpectedSourceLang ?? await detector.DetectAsync(sampleText);
        var enableTranslation = !string.IsNullOrWhiteSpace(sourceLang) &&
                                !string.Equals(sourceLang, settings.TargetLanguage, StringComparison.OrdinalIgnoreCase);

        var request = new AddSourceRequest
        {
            Title = item.Title,
            Url = feedUrl,
            TargetLanguage = settings.TargetLanguage,
            TranslateTitle = enableTranslation,
            TranslateContent = enableTranslation,
            GenerateSummary = !string.IsNullOrWhiteSpace(settings.DefaultSummarizerOption),
            TranslatorOption = enableTranslation ? settings.DefaultTranslatorOption : string.Empty,
            SummarizerOption = settings.DefaultSummarizerOption,
            TranslationDisplay = settings.DefaultTranslationDisplay,
            Tags = FilterNumericTags(item.Tags),
            Filters = ResolveDefaultFilters(settings)
        };

        var session = new RssBoxSessionSettings
        {
            BaseUrl = settings.RssBoxBaseUrl,
            Username = settings.RssBoxUsername,
            Password = settings.RssBoxPassword,
            Headless = true
        };

        return await _rssBoxClient.AddSourceAsync(session, request);
    }

    private IReadOnlyList<RssBoxOptionItem> FilterNumericTags(IReadOnlyList<string> tags)
    {
        if (tags.Count == 0)
        {
            return Array.Empty<RssBoxOptionItem>();
        }

        var normalized = tags
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim())
            .ToList();

        var numeric = normalized
            .Where(IsNumericId)
            .Select(tag => new RssBoxOptionItem(tag, tag))
            .ToList();

        if (numeric.Count != normalized.Count)
        {
            var skipped = string.Join(", ", normalized.Where(tag => !IsNumericId(tag)));
            _ = _logger.WarnAsync("Skipping non-numeric tag ids from recommendation.", skipped);
        }

        return numeric;
    }

    private static bool IsNumericId(string value) => value.All(char.IsDigit);

    private static bool TryValidateGeneratedFeedUrl(string? feedUrl, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(feedUrl))
        {
            error = "Feed URL is empty.";
            return false;
        }

        if (!Uri.TryCreate(feedUrl, UriKind.Absolute, out var uri))
        {
            error = "Feed URL is not absolute.";
            return false;
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            error = $"Feed URL scheme is invalid: {uri.Scheme}.";
            return false;
        }

        return true;
    }

    private IReadOnlyList<RssBoxOptionItem> ResolveDefaultFilters(UserSettings settings)
    {
        var option = _settingsViewModel.ResolveFilterOption(settings.DefaultFilterOption);
        return option == null ? Array.Empty<RssBoxOptionItem>() : new[] { option };
    }

    private static string BuildAutomationError(string prefix, OperationResult result)
    {
        if (!string.IsNullOrWhiteSpace(result.Detail))
        {
            return $"{prefix}: {result.Error}. Screenshot: {result.Detail}";
        }

        return $"{prefix}: {result.Error}";
    }
}
