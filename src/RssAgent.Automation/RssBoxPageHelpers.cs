using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace RssAgent.Automation;

internal static class RssBoxPageHelpers
{
    public static async Task<bool> TryFillByLabelAsync(IPage page, IReadOnlyList<string> labels, string value, int timeoutMs)
    {
        foreach (var label in labels)
        {
            var locator = page.GetByLabel(label, new PageGetByLabelOptions { Exact = false });
            if (await locator.CountAsync() > 0)
            {
                await locator.First.FillAsync(value, new LocatorFillOptions { Timeout = timeoutMs });
                return true;
            }
        }

        return false;
    }

    public static async Task<bool> TryClickByRoleAsync(IPage page, AriaRole role, IReadOnlyList<string> names, int timeoutMs)
    {
        foreach (var name in names)
        {
            var locator = page.GetByRole(role, new PageGetByRoleOptions { Name = name, Exact = false });
            if (await locator.CountAsync() > 0)
            {
                await locator.First.ClickAsync(new LocatorClickOptions { Timeout = timeoutMs });
                return true;
            }
        }

        return false;
    }
}
