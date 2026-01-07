using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Playwright;
using RssAgent.Core.Models;

namespace RssAgent.Automation.Pages;

internal sealed class TagsPage
{
    private static readonly Regex IdRegex = new(@"/core/tag/(?<id>\\d+)/change", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly IPage _page;

    public TagsPage(IPage page)
    {
        _page = page;
    }

    public async Task NavigateAsync(string baseUrl, int timeoutMs)
    {
        await _page.GotoAsync($"{baseUrl.TrimEnd('/')}/core/tag/", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.DOMContentLoaded,
            Timeout = timeoutMs
        });
    }

    public async Task<IReadOnlyList<TagInfo>> ReadTagsAsync(int timeoutMs)
    {
        await EnsureListPageAsync(timeoutMs);

        var rows = await _page.Locator(RssBoxDom.TagListRowSelector).AllAsync();
        var tags = new List<TagInfo>();

        foreach (var row in rows)
        {
            var link = row.Locator(RssBoxDom.TagNameSelector).First;
            var name = (await link.InnerTextAsync()).Trim();
            var href = await link.GetAttributeAsync("href") ?? string.Empty;
            var idMatch = IdRegex.Match(href);
            var id = idMatch.Success ? idMatch.Groups["id"].Value : null;

            if (!string.IsNullOrWhiteSpace(name))
            {
                tags.Add(new TagInfo
                {
                    Id = id,
                    Name = name
                });
            }
        }

        return tags;
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
