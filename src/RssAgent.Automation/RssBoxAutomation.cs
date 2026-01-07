using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;
using RssAgent.Automation.Pages;
using RssAgent.Core;
using RssAgent.Core.Logging;
using RssAgent.Core.Models;

namespace RssAgent.Automation;

public sealed class RssBoxAutomation
{
    private const int NavigationTimeoutMs = 25000;
    private const int ActionTimeoutMs = 15000;

    private readonly AppLogger _logger;
    private sealed record SaveResponseInfo(int Status, string Url, string Body);

    public RssBoxAutomation(AppLogger logger)
    {
        _logger = logger;
    }

    public Task<IReadOnlyList<FeedInfo>> GetFeedsAsync(RssBoxSessionSettings session, CancellationToken cancellationToken = default)
        => RunAsync(session, "feeds", async page =>
        {
            var feedsPage = new FeedsPage(page);
            await feedsPage.NavigateAsync(session.BaseUrl, NavigationTimeoutMs);
            return await feedsPage.ReadFeedsAsync(ActionTimeoutMs);
        });

    public Task<IReadOnlyList<TagInfo>> GetTagsAsync(RssBoxSessionSettings session, CancellationToken cancellationToken = default)
        => RunAsync(session, "tags", async page =>
        {
            var tagsPage = new TagsPage(page);
            await tagsPage.NavigateAsync(session.BaseUrl, NavigationTimeoutMs);
            return await tagsPage.ReadTagsAsync(ActionTimeoutMs);
        });

    public Task<IReadOnlyList<DigestInfo>> GetDigestsAsync(RssBoxSessionSettings session, CancellationToken cancellationToken = default)
        => RunAsync(session, "digests", async page =>
        {
            var digestsPage = new DigestsPage(page);
            await digestsPage.NavigateAsync(session.BaseUrl, NavigationTimeoutMs);
            return await digestsPage.ReadDigestsAsync(ActionTimeoutMs);
        });

    public Task<RssBoxOptionSet> GetOptionsAsync(RssBoxSessionSettings session, CancellationToken cancellationToken = default)
        => RunAsync(session, "options", async page =>
        {
            var addFeedPage = new AddFeedPage(page, _logger);
            await addFeedPage.NavigateAsync(session.BaseUrl, NavigationTimeoutMs);
            var options = await addFeedPage.ReadOptionsAsync(ActionTimeoutMs);

            var filtersPage = new FiltersPage(page);
            await filtersPage.NavigateAsync(session.BaseUrl, NavigationTimeoutMs);
            options.FilterOptions = await filtersPage.ReadFiltersAsync(ActionTimeoutMs);

            return options;
        });


    public Task<RssBoxSnapshot> GetSnapshotAsync(RssBoxSessionSettings session, CancellationToken cancellationToken = default)
        => RunAsync(session, "snapshot", async page =>
        {
            var feedsPage = new FeedsPage(page);
            await feedsPage.NavigateAsync(session.BaseUrl, NavigationTimeoutMs);
            var feeds = await feedsPage.ReadFeedsAsync(ActionTimeoutMs);

            var tagsPage = new TagsPage(page);
            await tagsPage.NavigateAsync(session.BaseUrl, NavigationTimeoutMs);
            var tags = await tagsPage.ReadTagsAsync(ActionTimeoutMs);

            var digestsPage = new DigestsPage(page);
            await digestsPage.NavigateAsync(session.BaseUrl, NavigationTimeoutMs);
            var digests = await digestsPage.ReadDigestsAsync(ActionTimeoutMs);

            var addFeedPage = new AddFeedPage(page, _logger);
            await addFeedPage.NavigateAsync(session.BaseUrl, NavigationTimeoutMs);
            var options = await addFeedPage.ReadOptionsAsync(ActionTimeoutMs);

            var filtersPage = new FiltersPage(page);
            await filtersPage.NavigateAsync(session.BaseUrl, NavigationTimeoutMs);
            options.FilterOptions = await filtersPage.ReadFiltersAsync(ActionTimeoutMs);

            return new RssBoxSnapshot
            {
                Feeds = feeds,
                Tags = tags,
                Digests = digests,
                Options = options
            };
        });

    public async Task<OperationResult> AddSourceAsync(RssBoxSessionSettings session, AddSourceRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            return await RunAsync(session, "add-feed", async page =>
            {
                var addFeedPage = new AddFeedPage(page, _logger);
                await addFeedPage.NavigateAsync(session.BaseUrl, NavigationTimeoutMs);
                await EnsureAddPageCleanAsync(page);

                var urlInputInfo = await addFeedPage.FillAsync(request, ActionTimeoutMs);
                await _logger.InfoAsync(
                    "RSSBox feed URL input.",
                    $"name={urlInputInfo.Name} id={urlInputInfo.Id} type={urlInputInfo.Type} placeholder={urlInputInfo.Placeholder}");
                await addFeedPage.EnsureReadyToSaveAsync(ActionTimeoutMs);

                var saveResponse = await CaptureSaveResponseAsync(page, addFeedPage, session.BaseUrl);

                var validationError = await addFeedPage.TryReadValidationErrorsAsync(ActionTimeoutMs);
                if (!string.IsNullOrWhiteSpace(validationError))
                {
                    var screenshotPath = await TryCaptureFailureScreenshotAsync(page, "add-validate");
                    return OperationResult.Fail($"Save validation failed: {validationError}", screenshotPath);
                }

                if (saveResponse == null)
                {
                    var screenshotPath = await TryCaptureFailureScreenshotAsync(page, "add-no-response");
                    return OperationResult.Fail("Save response was not captured.", screenshotPath);
                }

                if (!IsSuccessStatus(saveResponse.Status))
                {
                    var screenshotPath = await TryCaptureFailureScreenshotAsync(page, "add-response-failed");
                    return OperationResult.Fail($"Save response failed with status {saveResponse.Status}.", screenshotPath);
                }

                return OperationResult.Ok();
            });
        }
        catch (RssBoxAutomationException ex)
        {
            return OperationResult.Fail(ex.Message, ex.ScreenshotPath);
        }
    }

    private async Task<SaveResponseInfo?> CaptureSaveResponseAsync(IPage page, AddFeedPage addFeedPage, string baseUrl)
    {
        var addUrl = $"{baseUrl.TrimEnd('/')}/core/feed/add/";
        try
        {
            await EnsureCleanPageUrlBeforeSaveAsync(page);
            var preSaveUrl = page.Url;

            var response = await page.RunAndWaitForResponseAsync(
                async () => await addFeedPage.SaveAsync(ActionTimeoutMs),
                resp => IsSaveResponse(resp, addUrl),
                new PageRunAndWaitForResponseOptions { Timeout = NavigationTimeoutMs });

            var postSaveUrl = page.Url;
            await LogSaveRequestAsync(response, preSaveUrl, postSaveUrl);

            var body = await ReadResponseBodyAsync(response);
            await LogSaveResponseAsync(response, body);
            return new SaveResponseInfo(response.Status, response.Url, body);
        }
        catch (TimeoutException ex)
        {
            await _logger.WarnAsync("Timed out waiting for RSSBox save response.", ex.Message);
            return null;
        }
    }

    private async Task EnsureAddPageCleanAsync(IPage page)
    {
        var query = GetQuery(page.Url);
        if (string.IsNullOrWhiteSpace(query))
        {
            return;
        }

        await _logger.WarnAsync("RSSBox add page URL contains query.", $"{page.Url} | Query: {query}");
        throw new InvalidOperationException($"RSSBox add page URL contains query: {query}");
    }

    private async Task EnsureCleanPageUrlBeforeSaveAsync(IPage page)
    {
        var query = GetQuery(page.Url);
        if (string.IsNullOrWhiteSpace(query))
        {
            return;
        }

        await _logger.WarnAsync("RSSBox add page URL contains query before save.", $"{page.Url} | Query: {query}");
        await page.EvaluateAsync("() => history.replaceState(null, '', location.pathname)");

        var cleanedQuery = GetQuery(page.Url);
        if (!string.IsNullOrWhiteSpace(cleanedQuery))
        {
            throw new InvalidOperationException($"RSSBox add page URL still contains query: {cleanedQuery}");
        }

        await _logger.InfoAsync("RSSBox add page URL cleaned.", page.Url);
    }

    private async Task LogSaveRequestAsync(IResponse response, string preSaveUrl, string postSaveUrl)
    {
        var request = response.Request;
        var requestUrl = request.Url;
        var query = GetQuery(requestUrl);
        var postData = request.PostData ?? string.Empty;

        await _logger.InfoAsync(
            "RSSBox save request.",
            $"Method {request.Method} {requestUrl} | Query: {query} | PostData: {postData}");
        await _logger.InfoAsync("RSSBox page URL before save.", $"{preSaveUrl} | Query: {GetQuery(preSaveUrl)}");
        await _logger.InfoAsync("RSSBox page URL after save.", $"{postSaveUrl} | Query: {GetQuery(postSaveUrl)}");
    }

    private static bool IsSaveResponse(IResponse response, string addUrl)
    {
        if (!string.Equals(response.Request.Method, "POST", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return response.Url.StartsWith(addUrl, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<string> ReadResponseBodyAsync(IResponse response)
    {
        try
        {
            return await response.TextAsync();
        }
        catch (Exception ex)
        {
            await _logger.WarnAsync("Failed to read RSSBox save response body.", ex.Message);
            return string.Empty;
        }
    }

    private async Task LogSaveResponseAsync(IResponse response, string body)
    {
        var status = response.Status;
        var detail = $"Status {status} {response.Url}";
        if (IsSuccessStatus(status))
        {
            var snippet = TrimForLog(body, 2000);
            await _logger.InfoAsync("RSSBox save response.", $"{detail} | Body: {snippet}");
        }
        else
        {
            await _logger.WarnAsync("RSSBox save response failed.", $"{detail} | Body: {body}");
        }
    }

    private static string TrimForLog(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        {
            return value;
        }

        return value.Substring(0, maxLength) + "...(truncated)";
    }

    private static bool IsSuccessStatus(int status)
        => status == 200 || status == 201 || status == 302;

    private async Task<OperationResult> ConfirmAddAsync(IPage page, string baseUrl, AddSourceRequest request)
    {
        var feedsPage = new FeedsPage(page);
        if (!await IsFeedListAsync(page))
        {
            await feedsPage.NavigateAsync(baseUrl, NavigationTimeoutMs);
        }

        var feeds = await feedsPage.ReadFeedsAsync(ActionTimeoutMs);
        if (IsMatch(feeds, request))
        {
            return OperationResult.Ok();
        }

        if (!string.IsNullOrWhiteSpace(request.Url))
        {
            await feedsPage.NavigateToSearchAsync(baseUrl, request.Url, NavigationTimeoutMs);
            feeds = await feedsPage.ReadFeedsAsync(ActionTimeoutMs);
            if (IsMatch(feeds, request))
            {
                return OperationResult.Ok();
            }
        }

        if (!string.IsNullOrWhiteSpace(request.Title))
        {
            await feedsPage.NavigateToSearchAsync(baseUrl, request.Title, NavigationTimeoutMs);
            feeds = await feedsPage.ReadFeedsAsync(ActionTimeoutMs);
            if (IsMatch(feeds, request))
            {
                return OperationResult.Ok();
            }
        }

        var confirmScreenshot = await TryCaptureFailureScreenshotAsync(page, "add-confirm");
        return OperationResult.Fail("保存后未在列表中找到新增源。", confirmScreenshot);
    }

    private static async Task<bool> IsFeedListAsync(IPage page)
    {
        return await page.Locator(RssBoxDom.FeedListTableSelector).CountAsync() > 0;
    }

    private static bool IsMatch(IEnumerable<FeedInfo> feeds, AddSourceRequest request)
    {
        foreach (var feed in feeds)
        {
            if (!string.IsNullOrWhiteSpace(request.Url) && UrlEquals(feed.Url, request.Url))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(request.Title) &&
                !string.IsNullOrWhiteSpace(feed.Title) &&
                string.Equals(feed.Title.Trim(), request.Title.Trim(), StringComparison.OrdinalIgnoreCase))
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

    private async Task<T> RunAsync<T>(RssBoxSessionSettings session, string stage, Func<IPage, Task<T>> action)
    {
        var screenshotDir = PrepareScreenshotDirectory();
        var traceDir = PrepareTraceDirectory();
        var tracePath = Path.Combine(traceDir, $"rssbox-{stage}-{DateTimeOffset.Now:yyyyMMddHHmmss}.zip");

        session.BaseUrl = NormalizeBaseUrl(session.BaseUrl);
        var baseOrigin = GetOrigin(session.BaseUrl);

        IPage? page = null;
        IBrowserContext? context = null;
        IBrowser? browser = null;
        IPlaywright? playwright = null;
        Exception? caught = null;
        string? screenshotPath = null;
        T result = default!;

        try
        {
            playwright = await Playwright.CreateAsync();
            browser = await LaunchBrowserAsync(playwright, session.Headless);

            context = await browser.NewContextAsync(new BrowserNewContextOptions
            {
                BaseURL = session.BaseUrl,
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

            page = await context.NewPageAsync();
            page.SetDefaultTimeout(ActionTimeoutMs);
            page.SetDefaultNavigationTimeout(NavigationTimeoutMs);

            AttachPageDiagnostics(page, baseOrigin);

            await _logger.InfoAsync("Using RSSBox base URL.", session.BaseUrl);

            await page.GotoAsync(session.BaseUrl, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded,
                Timeout = NavigationTimeoutMs
            });

            await EnsureOriginMatchAsync(page, baseOrigin);

            await LoginIfNeededAsync(page, session);
            await EnsureOriginMatchAsync(page, baseOrigin);

            result = await action(page);
        }
        catch (Exception ex)
        {
            caught = ex;
        }
        finally
        {
            if (caught != null && page != null)
            {
                try
                {
                    screenshotPath = await TryCaptureScreenshotAsync(page, screenshotDir, stage);
                }
                catch (Exception screenshotEx)
                {
                    await _logger.WarnAsync("Failed to capture automation screenshot.", screenshotEx.Message);
                }
            }

            if (context != null)
            {
                await StopTracingAsync(context, tracePath);
                await CloseContextAsync(context);
            }

            if (browser != null)
            {
                try
                {
                    await browser.CloseAsync();
                }
                catch (Exception ex)
                {
                    await _logger.WarnAsync("Failed to close Playwright browser.", ex.Message);
                }
            }

            if (playwright != null)
            {
                playwright.Dispose();
            }
        }

        if (caught != null)
        {
            if (!string.IsNullOrWhiteSpace(screenshotPath))
            {
                await _logger.WarnAsync("Automation screenshot saved.", screenshotPath);
            }

            await _logger.ErrorAsync($"RSSBox automation failed: {stage}", caught.Message);
            throw new RssBoxAutomationException(stage, caught.Message, screenshotPath, caught);
        }

        return result;
    }
    private async Task LoginIfNeededAsync(IPage page, RssBoxSessionSettings session)
    {
        var loginPage = new LoginPage(page);
        if (!await loginPage.IsAtAsync())
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(session.Username) || string.IsNullOrWhiteSpace(session.Password))
        {
            throw new InvalidOperationException("RSSBox 需要登录，请在设置中填写用户名和密码。");
        }

        await loginPage.LoginAsync(session.Username, session.Password, ActionTimeoutMs);

        await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded, new PageWaitForLoadStateOptions
        {
            Timeout = NavigationTimeoutMs
        });
    }

    private async Task<IBrowser> LaunchBrowserAsync(IPlaywright playwright, bool headless)
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
            await _logger.WarnAsync("正在安装 Playwright 浏览器...", ex.Message);

            var exitCode = Microsoft.Playwright.Program.Main(new[] { "install", "chromium" });
            if (exitCode == 0)
            {
                await _logger.InfoAsync("Playwright 浏览器安装成功。", "chromium");
            }
            else
            {
                await _logger.ErrorAsync($"Playwright 安装失败，退出码 {exitCode}。");
            }

            return await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = headless
            });
        }
    }

    private static string NormalizeBaseUrl(string baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new InvalidOperationException("RSSBox base URL is required.");
        }

        var trimmed = baseUrl.Trim();
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException("RSSBox base URL must be absolute.");
        }

        var origin = uri.GetLeftPart(UriPartial.Authority);
        var path = uri.AbsolutePath.TrimEnd('/');
        if (!string.IsNullOrWhiteSpace(path) && path != "/")
        {
            return origin + path;
        }

        return origin;
    }

    private static string GetOrigin(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return uri.GetLeftPart(UriPartial.Authority);
        }

        return string.Empty;
    }

    private static string GetQuery(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return uri.Query ?? string.Empty;
        }

        return string.Empty;
    }

    private async Task EnsureOriginMatchAsync(IPage page, string baseOrigin)
    {
        var currentOrigin = GetOrigin(page.Url);
        if (string.IsNullOrWhiteSpace(currentOrigin))
        {
            return;
        }

        if (!string.Equals(currentOrigin, baseOrigin, StringComparison.OrdinalIgnoreCase))
        {
            await _logger.WarnAsync("RSSBox origin mismatch.", $"{currentOrigin} (expected {baseOrigin}). Ensure CSRF_TRUSTED_ORIGINS includes {baseOrigin}.");
            throw new InvalidOperationException($"RSSBox base URL origin mismatch. Expected {baseOrigin}, got {currentOrigin}.");
        }
    }

    private void AttachPageDiagnostics(IPage page, string baseOrigin)
    {
        page.RequestFailed += (_, request) =>
        {
            var failure = string.IsNullOrWhiteSpace(request.Failure) ? "unknown" : request.Failure;
            _ = _logger.WarnAsync("Playwright request failed.", $"{request.Method} {request.Url} | {failure}");
        };

        page.Console += (_, msg) =>
        {
            _ = _logger.InfoAsync("Page console.", $"{msg.Type}: {msg.Text}");
        };

        page.PageError += (_, message) =>
        {
            _ = _logger.WarnAsync("Page error.", message);
        };

        page.FrameNavigated += (_, frame) =>
        {
            if (frame != page.MainFrame)
            {
                return;
            }

            var origin = GetOrigin(frame.Url);
            if (string.IsNullOrWhiteSpace(origin) || string.Equals(origin, baseOrigin, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _ = _logger.WarnAsync("RSSBox navigation crossed origin.", $"{origin} (expected {baseOrigin}). Ensure CSRF_TRUSTED_ORIGINS includes {baseOrigin}.");
        };
    }

    private static string PrepareTraceDirectory()
    {
        var datedDir = Path.Combine(AppPaths.TracesDirectory, DateTimeOffset.Now.ToString("yyyyMMdd"));
        Directory.CreateDirectory(datedDir);
        return datedDir;
    }

    private async Task StopTracingAsync(IBrowserContext context, string tracePath)
    {
        try
        {
            await context.Tracing.StopAsync(new TracingStopOptions { Path = tracePath });
            await _logger.InfoAsync("Playwright trace saved.", tracePath);
        }
        catch (Exception ex)
        {
            await _logger.WarnAsync("Failed to save Playwright trace.", ex.Message);
        }
    }

    private async Task CloseContextAsync(IBrowserContext context)
    {
        try
        {
            await context.CloseAsync();
        }
        catch (Exception ex)
        {
            await _logger.WarnAsync("Failed to close Playwright context.", ex.Message);
        }
    }

    private static string PrepareScreenshotDirectory()
    {
        var datedDir = Path.Combine(AppPaths.ScreenshotsDirectory, DateTimeOffset.Now.ToString("yyyyMMdd"));
        Directory.CreateDirectory(datedDir);
        return datedDir;
    }

    private static async Task<string?> TryCaptureScreenshotAsync(IPage? page, string screenshotDir, string stage)
    {
        if (page == null)
        {
            return null;
        }

        var path = Path.Combine(screenshotDir, $"rssbox-{stage}-{DateTimeOffset.Now:yyyyMMddHHmmss}.png");
        await page.ScreenshotAsync(new PageScreenshotOptions { Path = path, FullPage = true });
        return path;
    }

    private static async Task<string?> TryCaptureFailureScreenshotAsync(IPage? page, string stage)
    {
        if (page == null)
        {
            return null;
        }

        try
        {
            return await TryCaptureScreenshotAsync(page, PrepareScreenshotDirectory(), stage);
        }
        catch
        {
            return null;
        }
    }
}
