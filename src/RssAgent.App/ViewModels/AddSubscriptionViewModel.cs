using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RssAgent.Automation;
using RssAgent.Core;
using RssAgent.Core.Logging;
using RssAgent.Core.Models;
using RssAgent.Integrations.Gemini;
using RssAgent.Integrations.LanguageDetection;
using RssAgent.Integrations.RssBox;
using RssAgent.Integrations.RssHub;

namespace RssAgent.App.ViewModels;

public partial class AddSubscriptionViewModel : ObservableObject
{
    private readonly SettingsViewModel _settingsViewModel;
    private readonly RssHubClient _rssHubClient;
    private readonly RssBoxClient _rssBoxClient;
    private readonly AppLogger _logger;

    public ObservableCollection<TagOptionViewModel> TagOptions { get; } = new();

    [ObservableProperty] private string _inputText = string.Empty;
    [ObservableProperty] private string _outputRssUrl = string.Empty;
    [ObservableProperty] private string _addedSourceName = string.Empty;
    [ObservableProperty] private string _extraTags = string.Empty;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isBusy;

    public AddSubscriptionViewModel(
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

    public void UpdateAvailableTags(IEnumerable<TagInfo> tags)
    {
        TagOptions.Clear();
        foreach (var tag in tags.Where(t => !string.IsNullOrWhiteSpace(t.Name)))
        {
            TagOptions.Add(new TagOptionViewModel(tag.Name, tag.Id));
        }
    }

    [RelayCommand]
    private async Task GenerateAndAddAsync()
    {
        if (string.IsNullOrWhiteSpace(InputText))
        {
            StatusMessage = "请输入订阅描述。";
            return;
        }

        var settings = _settingsViewModel.BuildUserSettings();
        var gemini = new GeminiClient(settings.GeminiApiKey, settings.GeminiModel, _logger);
        var detector = new CompositeLanguageDetector(new HeuristicLanguageDetector(), gemini);

        IsBusy = true;
        StatusMessage = "请求 Gemini...";
        try
        {
            var (plan, planResult) = await gemini.GeneratePlanAsync(InputText, "single");
            if (!planResult.Success || plan == null)
            {
                StatusMessage = planResult.Error ?? "Gemini 返回失败。";
                return;
            }

            var item = plan.Items[0];
            if (item.Tags.Count > 0)
            {
                ApplySuggestedTags(item.Tags);
            }

            var feedUrl = item.DirectRssUrl ?? _rssHubClient.BuildUrl(
                settings.RssHubBaseUrl,
                item.RssHub?.Route ?? string.Empty,
                item.RssHub?.Query);

            var urlSource = item.DirectRssUrl != null ? "Direct" : "RSSHub";
            if (!TryValidateGeneratedFeedUrl(feedUrl, out var validationError))
            {
                StatusMessage = "Generated feed URL is invalid/empty.";
                await _logger.ErrorAsync("GeneratedFeedUrl invalid.", $"{validationError} | Source: {urlSource} | Url: {feedUrl}");
                return;
            }

            await _logger.InfoAsync("GeneratedFeedUrl", $"{feedUrl} | Source: {urlSource}");
            OutputRssUrl = feedUrl;
            StatusMessage = "验证 RSSHub...";

            var validation = await _rssHubClient.ValidateFeedAsync(feedUrl);
            if (!validation.Success)
            {
                StatusMessage = $"RSSHub 验证失败: {validation.Error}";
                return;
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
                Tags = CollectTags(item.Tags),
                Filters = ResolveDefaultFilters(settings)
            };

            var session = new RssBoxSessionSettings
            {
                BaseUrl = settings.RssBoxBaseUrl,
                Username = settings.RssBoxUsername,
                Password = settings.RssBoxPassword,
                Headless = true
            };

            StatusMessage = "写入 RSSBox...";
            var result = await _rssBoxClient.AddSourceAsync(session, request);
            if (!result.Success)
            {
                StatusMessage = BuildAutomationError("Add to RSSBox failed", result);
                return;
            }

            AddedSourceName = request.Title;
            StatusMessage = "已添加到 RSSBox。";
        }
        catch (RssBoxAutomationException ex)
        {
            StatusMessage = BuildAutomationError("Add to RSSBox failed", ex);
            await _logger.ErrorAsync("Subscription flow failed.", ex.Message);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Flow failed: {ex.Message}";
            await _logger.ErrorAsync("Subscription flow failed.", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplySuggestedTags(IEnumerable<string> tags)
    {
        foreach (var option in TagOptions)
        {
            option.IsSelected = tags.Contains(option.Name, StringComparer.OrdinalIgnoreCase);
        }
    }

    private IReadOnlyList<RssBoxOptionItem> CollectTags(IEnumerable<string> suggested)
    {
        var tags = new Dictionary<string, RssBoxOptionItem>(StringComparer.OrdinalIgnoreCase);
        foreach (var tag in suggested)
        {
            var resolved = ResolveTagOption(tag);
            if (resolved != null)
            {
                tags[resolved.Label] = resolved;
            }
        }

        foreach (var option in TagOptions.Where(t => t.IsSelected))
        {
            var resolved = ResolveTagOption(option.Name);
            if (resolved != null)
            {
                tags[resolved.Label] = resolved;
            }
        }

        if (!string.IsNullOrWhiteSpace(ExtraTags))
        {
            var extras = ExtraTags.Split(",", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var extra in extras)
            {
                var resolved = ResolveTagOption(extra);
                if (resolved != null)
                {
                    tags[resolved.Label] = resolved;
                }
            }
        }

        return tags.Values.ToList();
    }

    private RssBoxOptionItem? ResolveTagOption(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return null;
        }

        var trimmed = tag.Trim();
        var match = TagOptions.FirstOrDefault(t => string.Equals(t.Name, trimmed, StringComparison.OrdinalIgnoreCase));
        if (match == null || !IsNumericId(match.Id))
        {
            _ = _logger.WarnAsync("Skipping tag without numeric id.", trimmed);
            return null;
        }

        return new RssBoxOptionItem(match.Id!, match.Name);
    }

    private static bool IsNumericId(string? value)
        => !string.IsNullOrWhiteSpace(value) && value.All(char.IsDigit);

    private IReadOnlyList<RssBoxOptionItem> ResolveDefaultFilters(UserSettings settings)
    {
        var option = _settingsViewModel.ResolveFilterOption(settings.DefaultFilterOption);
        return option == null ? Array.Empty<RssBoxOptionItem>() : new[] { option };
    }

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

    private static string BuildAutomationError(string prefix, OperationResult result)
    {
        if (!string.IsNullOrWhiteSpace(result.Detail))
        {
            return $"{prefix}: {result.Error}. Screenshot: {result.Detail}";
        }

        return $"{prefix}: {result.Error}";
    }

    private static string BuildAutomationError(string prefix, RssBoxAutomationException ex)
    {
        if (!string.IsNullOrWhiteSpace(ex.ScreenshotPath))
        {
            return $"{prefix}: {ex.Message}. Screenshot: {ex.ScreenshotPath}";
        }

        return $"{prefix}: {ex.Message}";
    }
}
