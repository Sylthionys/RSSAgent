using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Playwright;
using RssAgent.Core.Models;

namespace RssAgent.Automation.Pages;

internal sealed class FiltersPage
{
    private static readonly Regex IdRegex = new(@"/core/filter/(?<id>\\d+)/change", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly IPage _page;

    public FiltersPage(IPage page)
    {
        _page = page;
    }

    public async Task NavigateAsync(string baseUrl, int timeoutMs)
    {
        await _page.GotoAsync($"{baseUrl.TrimEnd('/')}/core/filter/", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.DOMContentLoaded,
            Timeout = timeoutMs
        });
    }

    public async Task<IReadOnlyList<RssBoxOptionItem>> ReadFiltersAsync(int timeoutMs)
    {
        await EnsureListPageAsync(timeoutMs);

        var rows = await _page.Locator(RssBoxDom.FilterListRowSelector).AllAsync();
        var filters = new List<RssBoxOptionItem>();

        foreach (var row in rows)
        {
            var link = row.Locator(RssBoxDom.FilterNameSelector).First;
            var name = (await link.InnerTextAsync()).Trim();
            var href = await link.GetAttributeAsync("href") ?? string.Empty;
            var idMatch = IdRegex.Match(href);
            var id = idMatch.Success ? idMatch.Groups["id"].Value : null;

            if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(id))
            {
                filters.Add(new RssBoxOptionItem(id, name));
            }
        }

        return filters;
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
