using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Playwright;
using RssAgent.Core.Models;

namespace RssAgent.Automation.Pages;

internal sealed class DigestsPage
{
    private readonly IPage _page;

    public DigestsPage(IPage page)
    {
        _page = page;
    }

    public async Task NavigateAsync(string baseUrl, int timeoutMs)
    {
        await _page.GotoAsync($"{baseUrl.TrimEnd('/')}/core/digest/", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.DOMContentLoaded,
            Timeout = timeoutMs
        });
    }

    public async Task<IReadOnlyList<DigestInfo>> ReadDigestsAsync(int timeoutMs)
    {
        await EnsureListPageAsync(timeoutMs);

        var rows = await _page.Locator(RssBoxDom.DigestListRowSelector).AllAsync();
        var digests = new List<DigestInfo>();

        foreach (var row in rows)
        {
            var name = (await row.Locator(RssBoxDom.DigestNameSelector).First.InnerTextAsync()).Trim();
            var cells = await row.Locator("td").AllAsync();
            var schedule = cells.Count > 0 ? (await cells[0].InnerTextAsync()).Trim() : string.Empty;

            if (!string.IsNullOrWhiteSpace(name))
            {
                digests.Add(new DigestInfo
                {
                    Name = name,
                    Schedule = schedule
                });
            }
        }

        return digests;
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
