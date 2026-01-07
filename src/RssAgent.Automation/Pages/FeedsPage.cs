using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Playwright;
using RssAgent.Core.Models;

namespace RssAgent.Automation.Pages;

internal sealed class FeedsPage
{
    private readonly IPage _page;

    public FeedsPage(IPage page)
    {
        _page = page;
    }

    public async Task NavigateAsync(string baseUrl, int timeoutMs)
    {
        await _page.GotoAsync($"{baseUrl.TrimEnd('/')}/core/feed/", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.DOMContentLoaded,
            Timeout = timeoutMs
        });
    }

    public async Task NavigateToSearchAsync(string baseUrl, string query, int timeoutMs)
    {
        var encoded = Uri.EscapeDataString(query);
        await _page.GotoAsync($"{baseUrl.TrimEnd('/')}/core/feed/?q={encoded}", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.DOMContentLoaded,
            Timeout = timeoutMs
        });
    }

    public async Task ClickAddAsync(string baseUrl, int timeoutMs)
    {
        await _page.WaitForSelectorAsync("form#changelist-form", new PageWaitForSelectorOptions
        {
            Timeout = timeoutMs
        });

        if (await RssBoxPageHelpers.TryClickByRoleAsync(_page, AriaRole.Link, new[] { "澧炲姞", "Add" }, timeoutMs))
        {
            return;
        }

        var addLink = _page.Locator("a[href='/core/feed/add/']");
        if (await addLink.CountAsync() > 0)
        {
            await addLink.First.ClickAsync(new LocatorClickOptions { Timeout = timeoutMs });
            return;
        }

        await _page.GotoAsync($"{baseUrl.TrimEnd('/')}/core/feed/add/", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.DOMContentLoaded,
            Timeout = timeoutMs
        });
    }

    public async Task<IReadOnlyList<FeedInfo>> ReadFeedsAsync(int timeoutMs)
    {
        await EnsureListPageAsync(timeoutMs);

        var rows = await _page.Locator(RssBoxDom.FeedListRowSelector).AllAsync();
        var feeds = new List<FeedInfo>();

        foreach (var row in rows)
        {
            var title = (await row.Locator(RssBoxDom.FeedTitleSelector).First.InnerTextAsync()).Trim();
            var url = string.Empty;
            var urlLocator = row.Locator(RssBoxDom.FeedFetchUrlSelector);
            if (await urlLocator.CountAsync() > 0)
            {
                url = (await urlLocator.First.GetAttributeAsync("href")) ?? string.Empty;
            }

            var tagsText = string.Empty;
            var tagsLocator = row.Locator(RssBoxDom.FeedTagsSelector);
            if (await tagsLocator.CountAsync() > 0)
            {
                tagsText = (await tagsLocator.First.InnerTextAsync()).Trim();
            }

            var statusText = string.Empty;
            var statusLocator = row.Locator(RssBoxDom.FeedStatusSelector);
            if (await statusLocator.CountAsync() > 0)
            {
                statusText = (await statusLocator.First.InnerTextAsync()).Trim();
            }

            var tags = tagsText.Split(new[] { ',', '，', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

            feeds.Add(new FeedInfo
            {
                Title = title,
                Url = url,
                Tags = tags,
                Status = statusText
            });
        }

        return feeds;
    }

    private async Task EnsureListPageAsync(int timeoutMs)
    {
        if (await _page.Locator("form#login-form").CountAsync() > 0)
        {
            throw new InvalidOperationException("Login required or failed.");
        }

        await _page.WaitForSelectorAsync("form#changelist-form", new PageWaitForSelectorOptions
        {
            Timeout = timeoutMs
        });
    }
}

