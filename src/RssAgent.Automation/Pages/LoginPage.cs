using System.Threading.Tasks;
using Microsoft.Playwright;

namespace RssAgent.Automation.Pages;

internal sealed class LoginPage
{
    private readonly IPage _page;

    public LoginPage(IPage page)
    {
        _page = page;
    }

    public async Task<bool> IsAtAsync()
    {
        return await _page.Locator("form#login-form").CountAsync() > 0;
    }

    public async Task LoginAsync(string username, string password, int timeoutMs)
    {
        await RssBoxPageHelpers.TryFillByLabelAsync(_page, new[] { "用户名", "Username", "Email" }, username, timeoutMs);
        await RssBoxPageHelpers.TryFillByLabelAsync(_page, new[] { "密码", "Password" }, password, timeoutMs);

        var clicked = await RssBoxPageHelpers.TryClickByRoleAsync(_page, AriaRole.Button, new[] { "登录", "Login", "Sign in" }, timeoutMs);
        if (!clicked)
        {
            var submit = _page.Locator("form#login-form input[type='submit']");
            await submit.ClickAsync(new LocatorClickOptions { Timeout = timeoutMs });
        }
    }
}
