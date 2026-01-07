using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Playwright;
using RssAgent.Core;

internal sealed class ProbeResult
{
    public string BaseUrl { get; set; } = string.Empty;
    public string FeedUrl { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string AfterSaveUrl { get; set; } = string.Empty;
    public bool AfterSaveHasList { get; set; }
    public string? ValidationError { get; set; }
    public bool MatchInList { get; set; }
    public bool MatchInSearchByUrl { get; set; }
    public bool MatchInSearchByTitle { get; set; }
    public bool PaginationDetected { get; set; }
    public int ListCount { get; set; }
    public int SearchUrlCount { get; set; }
    public int SearchTitleCount { get; set; }
    public string OutputDir { get; set; } = string.Empty;
    public string TracePath { get; set; } = string.Empty;
}

internal sealed class FeedRow
{
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}

internal static class Program
{
    private const int NavigationTimeoutMs = 25000;
    private const int ActionTimeoutMs = 15000;

    private static async Task<int> Main(string[] args)
    {
        var options = ParseArgs(args);
        if (string.IsNullOrWhiteSpace(options.BaseUrl) ||
            string.IsNullOrWhiteSpace(options.Username) ||
            string.IsNullOrWhiteSpace(options.Password))
        {
            Console.WriteLine("Usage: --baseUrl <url> --username <user> --password <pass> [--feedUrl <url>] [--title <title>]");
            return 1;
        }

        var feedUrl = options.FeedUrl;
        if (string.IsNullOrWhiteSpace(feedUrl))
        {
            var stamp = DateTimeOffset.Now.ToString("yyyyMMddHHmmss");
            feedUrl = $"{options.RssHubBase}/twitter/user/rsshub?probe={stamp}";
        }

        var title = options.Title;
        if (string.IsNullOrWhiteSpace(title))
        {
            title = $"Probe {DateTimeOffset.Now:yyyyMMddHHmmss}";
        }

        var outputDir = Path.Combine(AppPaths.LogsDirectory, "probe", DateTimeOffset.Now.ToString("yyyyMMddHHmmss"));
        Directory.CreateDirectory(outputDir);
        var tracePath = Path.Combine(outputDir, "rssbox-trace.zip");

        var result = new ProbeResult
        {
            BaseUrl = options.BaseUrl,
            FeedUrl = feedUrl,
            Title = title,
            OutputDir = outputDir,
            TracePath = tracePath
        };

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await LaunchBrowserAsync(playwright, options.Headless);
        var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            Locale = "zh-CN",
            ExtraHTTPHeaders = new Dictionary<string, string>
            {
                ["Accept-Language"] = "zh-CN,zh;q=0.9"
            }
        });

        await context.Tracing.StartAsync(new TracingStartOptions
        {
            Screenshots = true,
            Snapshots = true,
            Sources = true
        });

        var page = await context.NewPageAsync();
        page.SetDefaultTimeout(ActionTimeoutMs);
        page.SetDefaultNavigationTimeout(NavigationTimeoutMs);

        await page.GotoAsync(options.BaseUrl, new PageGotoOptions
        {
            WaitUntil = WaitUntilState.DOMContentLoaded,
            Timeout = NavigationTimeoutMs
        });

        await LoginIfNeededAsync(page, options.Username, options.Password);

        await page.GotoAsync($"{options.BaseUrl.TrimEnd('/')}/core/feed/add/", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.DOMContentLoaded,
            Timeout = NavigationTimeoutMs
        });

        await page.WaitForSelectorAsync("#feed_form", new PageWaitForSelectorOptions
        {
            Timeout = ActionTimeoutMs
        });

        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(outputDir, "add-page.png"),
            FullPage = true
        });

        await page.Locator("#id_feed_url").FillAsync(feedUrl);
        await page.Locator("#id_name").FillAsync(title);

        await page.Locator("input[name='_save']").ClickAsync(new LocatorClickOptions { Timeout = ActionTimeoutMs });
        await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded, new PageWaitForLoadStateOptions
        {
            Timeout = NavigationTimeoutMs
        });

        result.AfterSaveUrl = page.Url;
        result.AfterSaveHasList = await page.Locator("#result_list").CountAsync() > 0;

        result.ValidationError = await ReadValidationErrorsAsync(page);

        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(outputDir, "after-save.png"),
            FullPage = true
        });

        if (!result.AfterSaveHasList)
        {
            await page.GotoAsync($"{options.BaseUrl.TrimEnd('/')}/core/feed/", new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded,
                Timeout = NavigationTimeoutMs
            });
        }

        result.PaginationDetected = await page.Locator(".paginator, .pagination").CountAsync() > 0;

        var feeds = await ReadFeedsAsync(page);
        result.ListCount = feeds.Count;
        result.MatchInList = IsMatch(feeds, feedUrl, title);

        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(outputDir, "feed-list.png"),
            FullPage = true
        });

        if (!result.MatchInList)
        {
            var encoded = Uri.EscapeDataString(feedUrl);
            await page.GotoAsync($"{options.BaseUrl.TrimEnd('/')}/core/feed/?q={encoded}", new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded,
                Timeout = NavigationTimeoutMs
            });
            var searchFeeds = await ReadFeedsAsync(page);
            result.SearchUrlCount = searchFeeds.Count;
            result.MatchInSearchByUrl = IsMatch(searchFeeds, feedUrl, title);
        }

        if (!result.MatchInList && !result.MatchInSearchByUrl)
        {
            var encoded = Uri.EscapeDataString(title);
            await page.GotoAsync($"{options.BaseUrl.TrimEnd('/')}/core/feed/?q={encoded}", new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded,
                Timeout = NavigationTimeoutMs
            });
            var searchFeeds = await ReadFeedsAsync(page);
            result.SearchTitleCount = searchFeeds.Count;
            result.MatchInSearchByTitle = IsMatch(searchFeeds, feedUrl, title);
        }

        await context.Tracing.StopAsync(new TracingStopOptions { Path = tracePath });

        var json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        var resultPath = Path.Combine(outputDir, "probe-result.json");
        await File.WriteAllTextAsync(resultPath, json);

        Console.WriteLine(json);
        Console.WriteLine($"Artifacts written to: {outputDir}");

        return 0;
    }

    private static async Task LoginIfNeededAsync(IPage page, string username, string password)
    {
        if (await page.Locator("form#login-form").CountAsync() == 0)
        {
            return;
        }

        await page.Locator("#id_username").FillAsync(username);
        await page.Locator("#id_password").FillAsync(password);
        await page.Locator("input[type='submit']").ClickAsync();

        await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded, new PageWaitForLoadStateOptions
        {
            Timeout = NavigationTimeoutMs
        });
    }

    private static async Task<string?> ReadValidationErrorsAsync(IPage page)
    {
        var errors = new List<string>();
        var errorItems = await page.Locator(".errorlist li").AllInnerTextsAsync();
        foreach (var error in errorItems)
        {
            if (!string.IsNullOrWhiteSpace(error))
            {
                errors.Add(error.Trim());
            }
        }

        var errorNote = await page.Locator(".errornote").AllInnerTextsAsync();
        foreach (var note in errorNote)
        {
            if (!string.IsNullOrWhiteSpace(note))
            {
                errors.Add(note.Trim());
            }
        }

        return errors.Count == 0 ? null : string.Join("; ", errors);
    }

    private static async Task<List<FeedRow>> ReadFeedsAsync(IPage page)
    {
        await page.WaitForSelectorAsync("form#changelist-form", new PageWaitForSelectorOptions
        {
            Timeout = ActionTimeoutMs
        });

        var rows = await page.Locator("#result_list tbody tr").AllAsync();
        var feeds = new List<FeedRow>();

        foreach (var row in rows)
        {
            var title = (await row.Locator("th.field-name a").First.InnerTextAsync()).Trim();
            var url = string.Empty;
            var urlLocator = row.Locator("td.field-fetch_feed a[href^='http']");
            if (await urlLocator.CountAsync() > 0)
            {
                url = (await urlLocator.First.GetAttributeAsync("href")) ?? string.Empty;
            }

            feeds.Add(new FeedRow
            {
                Title = title,
                Url = url
            });
        }

        return feeds;
    }

    private static bool IsMatch(IEnumerable<FeedRow> feeds, string url, string title)
    {
        foreach (var feed in feeds)
        {
            if (!string.IsNullOrWhiteSpace(url) && UrlEquals(feed.Url, url))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(title) &&
                !string.IsNullOrWhiteSpace(feed.Title) &&
                string.Equals(feed.Title.Trim(), title.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool UrlEquals(string? left, string? right)
    {
        var leftNorm = NormalizeUrl(left);
        var rightNorm = NormalizeUrl(right);

        if (string.IsNullOrWhiteSpace(leftNorm) || string.IsNullOrWhiteSpace(rightNorm))
        {
            return false;
        }

        if (string.Equals(leftNorm, rightNorm, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return leftNorm.Contains(rightNorm, StringComparison.OrdinalIgnoreCase) ||
               rightNorm.Contains(leftNorm, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim().TrimEnd('/');
        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var absolute))
        {
            return absolute.PathAndQuery.TrimEnd('/');
        }

        if (Uri.TryCreate(trimmed, UriKind.Relative, out var relative))
        {
            return relative.OriginalString.TrimEnd('/');
        }

        return trimmed;
    }

    private static async Task<IBrowser> LaunchBrowserAsync(IPlaywright playwright, bool headless)
    {
        try
        {
            return await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = headless
            });
        }
        catch (PlaywrightException ex) when (ex.Message.Contains("Executable doesn't exist", StringComparison.OrdinalIgnoreCase))
        {
            var exitCode = Microsoft.Playwright.Program.Main(new[] { "install", "chromium" });
            if (exitCode != 0)
            {
                throw;
            }

            return await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = headless
            });
        }
    }

    private static ProbeOptions ParseArgs(string[] args)
    {
        var options = new ProbeOptions();
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (!arg.StartsWith("--", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var key = arg.Substring(2);
            var value = i + 1 < args.Length ? args[i + 1] : string.Empty;
            if (string.IsNullOrWhiteSpace(value) || value.StartsWith("--", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            switch (key.ToLowerInvariant())
            {
                case "baseurl":
                    options.BaseUrl = value;
                    break;
                case "username":
                    options.Username = value;
                    break;
                case "password":
                    options.Password = value;
                    break;
                case "feedurl":
                    options.FeedUrl = value;
                    break;
                case "title":
                    options.Title = value;
                    break;
                case "rsshub":
                    options.RssHubBase = value.TrimEnd('/');
                    break;
                case "headless":
                    options.Headless = value.Equals("true", StringComparison.OrdinalIgnoreCase);
                    break;
            }
        }

        if (string.IsNullOrWhiteSpace(options.RssHubBase))
        {
            options.RssHubBase = "http://localhost:21200";
        }

        return options;
    }

    private sealed class ProbeOptions
    {
        public string BaseUrl { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string FeedUrl { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string RssHubBase { get; set; } = string.Empty;
        public bool Headless { get; set; } = true;
    }
}
