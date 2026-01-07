using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;
using RssAgent.Core.Logging;
using RssAgent.Core.Models;

namespace RssAgent.Automation.Pages;

internal sealed record FeedUrlInputInfo(string Name, string Id, string Type, string Placeholder);

internal sealed class AddFeedPage
{
    private readonly IPage _page;
    private readonly AppLogger _logger;
    private static readonly string[] SaveButtonNames = { "保存", "Save" };
    private static readonly string[] FeedUrlLabels = { "订阅链接", "订阅链接:", "Feed URL", "Feed Url", "RSS URL", "URL" };

    public AddFeedPage(IPage page, AppLogger logger)
    {
        _page = page;
        _logger = logger;
    }

    public async Task NavigateAsync(string baseUrl, int timeoutMs)
    {
        await _page.GotoAsync($"{baseUrl.TrimEnd('/')}/core/feed/add/", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.DOMContentLoaded,
            Timeout = timeoutMs
        });
    }

    public async Task<RssBoxOptionSet> ReadOptionsAsync(int timeoutMs)
    {
        if (await _page.Locator("form#login-form").CountAsync() > 0)
        {
            throw new InvalidOperationException("Login required or failed.");
        }

        await _page.WaitForSelectorAsync("#feed_form", new PageWaitForSelectorOptions
        {
            Timeout = timeoutMs
        });

        var translatorOptions = await ReadSelectOptionsAsync("#id_translator_option");
        var summarizerOptions = await ReadSelectOptionsAsync("#id_summarizer");
        var displayOptions = await ReadSelectOptionsAsync("#id_translation_display");
        var languageOptions = await ReadSelectOptionsAsync("#id_target_language");
        var selectedLanguage = await ReadSelectedOptionValueAsync("#id_target_language");

        return new RssBoxOptionSet
        {
            TranslatorOptions = translatorOptions,
            SummarizerOptions = summarizerOptions,
            TranslationDisplayOptions = displayOptions,
            TargetLanguageOptions = languageOptions,
            DefaultTargetLanguage = selectedLanguage
        };
    }

    public async Task<FeedUrlInputInfo> FillAsync(AddSourceRequest request, int timeoutMs)
    {
        if (await _page.Locator("form#login-form").CountAsync() > 0)
        {
            throw new InvalidOperationException("Login required or failed.");
        }

        await _page.WaitForSelectorAsync("#feed_form", new PageWaitForSelectorOptions
        {
            Timeout = timeoutMs
        });

        await ActivateTabAsync(new[] { "源信息", "Source" }, timeoutMs);

        var urlInput = await ResolveFeedUrlInputAsync(timeoutMs, requireVisible: true);
        var urlInputInfo = await ReadInputInfoAsync(urlInput);
        EnsureValidFeedUrlInput(urlInputInfo);
        await FillFeedUrlAsync(urlInput, request.Url, timeoutMs);

        if (!string.IsNullOrWhiteSpace(request.Title))
        {
            await _page.Locator("#id_name").FillAsync(request.Title, new LocatorFillOptions { Timeout = timeoutMs });
        }

        if (request.RefreshMinutes.HasValue && request.RefreshMinutes.Value > 0)
        {
            await _page.Locator("#id_simple_update_frequency").SelectOptionAsync(
                new[] { request.RefreshMinutes.Value.ToString() },
                new LocatorSelectOptionOptions { Timeout = timeoutMs });
        }

        await SetMultiSelectAsync("#id_tags", request.Tags, timeoutMs, requireNumericValues: true);

        await ActivateTabAsync(new[] { "内容处理", "Content" }, timeoutMs);

        if (!string.IsNullOrWhiteSpace(request.TargetLanguage))
        {
            await _page.Locator("#id_target_language").SelectOptionAsync(
                new[] { request.TargetLanguage },
                new LocatorSelectOptionOptions { Timeout = timeoutMs });
        }

        await SetCheckboxAsync("#id_translate_title", request.TranslateTitle, timeoutMs);
        await SetCheckboxAsync("#id_translate_content", request.TranslateContent, timeoutMs);
        await SetCheckboxAsync("#id_summary", request.GenerateSummary, timeoutMs);

        if (!string.IsNullOrWhiteSpace(request.TranslatorOption))
        {
            await _page.Locator("#id_translator_option").SelectOptionAsync(
                new[] { request.TranslatorOption },
                new LocatorSelectOptionOptions { Timeout = timeoutMs });
        }

        if (!string.IsNullOrWhiteSpace(request.SummarizerOption))
        {
            await _page.Locator("#id_summarizer").SelectOptionAsync(
                new[] { request.SummarizerOption },
                new LocatorSelectOptionOptions { Timeout = timeoutMs });
        }

        await ActivateTabAsync(new[] { "输出控制", "Output" }, timeoutMs);

        if (!string.IsNullOrWhiteSpace(request.TranslationDisplay))
        {
            await _page.Locator("#id_translation_display").SelectOptionAsync(
                new[] { request.TranslationDisplay },
                new LocatorSelectOptionOptions { Timeout = timeoutMs });
        }

        await SetMultiSelectAsync("#id_filters", request.Filters, timeoutMs, requireNumericValues: true);

        return urlInputInfo;
    }

    public async Task<string?> TryReadValidationErrorsAsync(int timeoutMs)
    {
        if (await _page.Locator("#feed_form").CountAsync() == 0)
        {
            return null;
        }

        var errors = new List<string>();
        var errorItems = await _page.Locator(".errorlist li").AllInnerTextsAsync();
        foreach (var error in errorItems)
        {
            if (!string.IsNullOrWhiteSpace(error))
            {
                errors.Add(error.Trim());
            }
        }

        var errorNote = await _page.Locator(".errornote").AllInnerTextsAsync();
        foreach (var note in errorNote)
        {
            if (!string.IsNullOrWhiteSpace(note))
            {
                errors.Add(note.Trim());
            }
        }

        return errors.Count == 0 ? null : string.Join("; ", errors);
    }

    public async Task EnsureReadyToSaveAsync(int timeoutMs)
    {
        await _page.WaitForSelectorAsync("#feed_form", new PageWaitForSelectorOptions
        {
            Timeout = timeoutMs
        });

        var urlInput = await ResolveFeedUrlInputAsync(timeoutMs, requireVisible: false);
        var urlInfo = await ReadInputInfoAsync(urlInput);
        EnsureValidFeedUrlInput(urlInfo);

        var urlValue = await urlInput.InputValueAsync(new LocatorInputValueOptions { Timeout = timeoutMs });
        if (string.IsNullOrWhiteSpace(urlValue))
        {
            throw new InvalidOperationException("Feed URL is required before saving.");
        }

        var saveButton = await FindSaveButtonAsync();
        if (saveButton == null)
        {
            throw new InvalidOperationException("Save button was not found.");
        }

        if (!await saveButton.IsEnabledAsync())
        {
            throw new InvalidOperationException("Save button is disabled.");
        }
    }

    public async Task SaveAsync(int timeoutMs)
    {
        var saveButton = await FindSaveButtonAsync();
        if (saveButton != null)
        {
            await saveButton.ClickAsync(new LocatorClickOptions { Timeout = timeoutMs });
            await _page.WaitForLoadStateAsync(LoadState.DOMContentLoaded, new PageWaitForLoadStateOptions { Timeout = timeoutMs });
            return;
        }

        var clicked = await RssBoxPageHelpers.TryClickByRoleAsync(_page, AriaRole.Button, SaveButtonNames, timeoutMs);
        if (!clicked)
        {
            await _page.GetByText(SaveButtonNames[0], new PageGetByTextOptions { Exact = false }).ClickAsync(new LocatorClickOptions { Timeout = timeoutMs });
        }
        await _page.WaitForLoadStateAsync(LoadState.DOMContentLoaded, new PageWaitForLoadStateOptions { Timeout = timeoutMs });
    }

    private async Task ActivateTabAsync(IReadOnlyList<string> names, int timeoutMs)
    {
        foreach (var name in names)
        {
            var locator = _page.Locator($".tab-nav li:has-text(\"{name}\")");
            if (await locator.CountAsync() > 0)
            {
                await locator.First.ClickAsync(new LocatorClickOptions { Timeout = timeoutMs });
                return;
            }
        }
    }

    private async Task<IReadOnlyList<RssBoxOptionItem>> ReadSelectOptionsAsync(string selector)
    {
        var options = new List<RssBoxOptionItem>();
        var optionNodes = await _page.Locator($"{selector} option").AllAsync();

        foreach (var option in optionNodes)
        {
            var value = await option.GetAttributeAsync("value") ?? string.Empty;
            var label = (await option.InnerTextAsync()).Trim();
            options.Add(new RssBoxOptionItem(value, label));
        }

        return options;
    }

    private async Task<string?> ReadSelectedOptionValueAsync(string selector)
    {
        var selected = _page.Locator($"{selector} option[selected]");
        if (await selected.CountAsync() > 0)
        {
            return await selected.First.GetAttributeAsync("value");
        }

        return null;
    }

    private async Task SetCheckboxAsync(string selector, bool value, int timeoutMs)
    {
        var checkbox = _page.Locator(selector);
        if (await checkbox.CountAsync() == 0)
        {
            return;
        }

        var isChecked = await checkbox.IsCheckedAsync();
        if (isChecked != value)
        {
            await checkbox.ClickAsync(new LocatorClickOptions { Timeout = timeoutMs });
        }
    }

    private async Task SetMultiSelectAsync(string selector, IReadOnlyList<RssBoxOptionItem> items, int timeoutMs, bool requireNumericValues = false)
    {
        if (items.Count == 0)
        {
            return;
        }

        var select = _page.Locator(selector);
        if (await select.CountAsync() == 0)
        {
            return;
        }

        var payload = items
            .Where(item => !string.IsNullOrWhiteSpace(item.Value))
            .Where(item => !requireNumericValues || IsNumeric(item.Value))
            .Select(item => new { value = item.Value, label = item.Label })
            .ToArray();

        if (payload.Length == 0)
        {
            return;
        }

        await select.EvaluateAsync(
            @"(element, values) => {
                const select = element;
                values.forEach(item => {
                    let option = Array.from(select.options).find(o => o.value === item.value);
                    if (!option) {
                        option = new Option(item.label, item.value, true, true);
                        select.add(option);
                    } else {
                        option.selected = true;
                    }
                });
                select.dispatchEvent(new Event('change', { bubbles: true }));
            }",
            payload);
    }

    private async Task FillFeedUrlAsync(ILocator locator, string url, int timeoutMs)
    {
        await LogInputStateAsync(locator, "before-fill");

        await Expect(locator).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = timeoutMs });
        await Expect(locator).ToBeEnabledAsync(new LocatorAssertionsToBeEnabledOptions { Timeout = timeoutMs });

        await locator.FillAsync(url, new LocatorFillOptions { Timeout = timeoutMs });
        await locator.PressAsync("Tab", new LocatorPressOptions { Timeout = timeoutMs });

        var value = await ReadAndLogInputValueAsync(locator, "after-fill");
        if (string.IsNullOrWhiteSpace(value))
        {
            await TryFallbackTypeAsync(locator, url, timeoutMs);
            value = await ReadAndLogInputValueAsync(locator, "after-fallback");
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            await DumpPageStateAsync("Feed URL input is empty after fallback.");
            throw new InvalidOperationException("Feed URL input is empty after fallback.");
        }

        await Expect(locator).ToHaveValueAsync(new Regex(".+"), new LocatorAssertionsToHaveValueOptions { Timeout = 5000 });
        await WaitForStableInputValueAsync(locator, 2000, 400, 200);
    }

    private async Task LogInputStateAsync(ILocator locator, string stage)
    {
        var visible = await locator.IsVisibleAsync();
        var enabled = await locator.IsEnabledAsync();
        var box = await locator.BoundingBoxAsync();
        var boxText = box == null
            ? "null"
            : $"x={box.X:0.##} y={box.Y:0.##} w={box.Width:0.##} h={box.Height:0.##}";

        await _logger.InfoAsync("RSSBox feed URL input state.", $"{stage} visible={visible} enabled={enabled} box={boxText}");
    }

    private async Task<string> ReadAndLogInputValueAsync(ILocator locator, string stage)
    {
        var value = await locator.InputValueAsync();
        var preview = FormatValuePreview(value, 50);
        await _logger.InfoAsync("RSSBox feed URL input value.", $"{stage} length={value.Length} value={preview}");
        return value;
    }

    private static string FormatValuePreview(string value, int span)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "<empty>";
        }

        if (value.Length <= span * 2)
        {
            return value;
        }

        return $"{value[..span]}...{value[^span..]}";
    }

    private async Task TryFallbackTypeAsync(ILocator locator, string url, int timeoutMs)
    {
        await locator.ClickAsync(new LocatorClickOptions { Timeout = timeoutMs });
        await locator.PressAsync("Control+A", new LocatorPressOptions { Timeout = timeoutMs });
        await locator.PressSequentiallyAsync(url, new LocatorPressSequentiallyOptions { Delay = 10, Timeout = timeoutMs });
        await locator.PressAsync("Tab", new LocatorPressOptions { Timeout = timeoutMs });
    }

    private async Task WaitForStableInputValueAsync(ILocator locator, int timeoutMs, int stableMs, int intervalMs)
    {
        var total = Stopwatch.StartNew();
        var stable = Stopwatch.StartNew();
        var last = await locator.InputValueAsync();

        while (total.ElapsedMilliseconds < timeoutMs)
        {
            await Task.Delay(intervalMs);
            var current = await locator.InputValueAsync();
            if (!string.Equals(current, last, StringComparison.Ordinal))
            {
                last = current;
                stable.Restart();
            }

            if (!string.IsNullOrWhiteSpace(current) && stable.ElapsedMilliseconds >= stableMs)
            {
                await _logger.InfoAsync("RSSBox feed URL input stabilized.", $"length={current.Length}");
                return;
            }
        }

        throw new InvalidOperationException("Feed URL input value did not stabilize.");
    }

    private async Task DumpPageStateAsync(string reason)
    {
        try
        {
            await _logger.WarnAsync("RSSBox feed URL input check failed.", reason);
            await _logger.WarnAsync("RSSBox page URL.", _page.Url);
            var content = await _page.ContentAsync();
            await _logger.WarnAsync("RSSBox page content dump.", content);
        }
        catch (Exception ex)
        {
            await _logger.WarnAsync("Failed to dump RSSBox page state.", ex.Message);
        }
    }

    private async Task<ILocator> ResolveFeedUrlInputAsync(int timeoutMs, bool requireVisible)
    {
        foreach (var label in FeedUrlLabels)
        {
            var locator = _page.GetByLabel(label, new PageGetByLabelOptions { Exact = true });
            if (await IsUsableInputAsync(locator, requireVisible))
            {
                return locator.First;
            }
        }

        foreach (var label in FeedUrlLabels)
        {
            var locator = _page.Locator($"input[placeholder=\"{label}\"], input[aria-label=\"{label}\"]");
            if (await IsUsableInputAsync(locator, requireVisible))
            {
                return locator.First;
            }
        }

        var fallback = _page.Locator("#id_feed_url");
        if (await IsUsableInputAsync(fallback, requireVisible))
        {
            return fallback.First;
        }

        throw new InvalidOperationException("Feed URL input is missing.");
    }

    private static async Task<bool> IsUsableInputAsync(ILocator locator, bool requireVisible)
    {
        if (await locator.CountAsync() == 0)
        {
            return false;
        }

        return !requireVisible || await locator.First.IsVisibleAsync();
    }

    private async Task<FeedUrlInputInfo> ReadInputInfoAsync(ILocator input)
    {
        var name = await input.GetAttributeAsync("name") ?? string.Empty;
        var id = await input.GetAttributeAsync("id") ?? string.Empty;
        var type = await input.GetAttributeAsync("type") ?? string.Empty;
        var placeholder = await input.GetAttributeAsync("placeholder") ?? string.Empty;
        return new FeedUrlInputInfo(name, id, type, placeholder);
    }

    private static void EnsureValidFeedUrlInput(FeedUrlInputInfo info)
    {
        if (string.Equals(info.Name, "id", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Feed URL input resolved to name=\"id\".");
        }

        if (string.Equals(info.Type, "hidden", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Feed URL input resolved to a hidden field.");
        }
    }

    private static bool IsNumeric(string value) => value.All(char.IsDigit);

    private async Task<ILocator?> FindSaveButtonAsync()
    {
        var saveButton = _page.Locator("input[name='_save']");
        if (await saveButton.CountAsync() > 0)
        {
            return saveButton.First;
        }

        foreach (var name in SaveButtonNames)
        {
            var byRole = _page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = name, Exact = false });
            if (await byRole.CountAsync() > 0)
            {
                return byRole.First;
            }
        }

        return null;
    }
}
