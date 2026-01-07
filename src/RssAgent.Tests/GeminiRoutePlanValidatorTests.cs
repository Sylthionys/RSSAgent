using RssAgent.Core.Models;
using RssAgent.Core.Services;
using Xunit;

namespace RssAgent.Tests;

public class GeminiRoutePlanValidatorTests
{
    [Fact]
    public void Validate_SingleModeRequiresOneItem()
    {
        var plan = new GeminiRoutePlan
        {
            Mode = "single",
            Items = { new GeminiRouteItem { Title = "Test", RssHub = new GeminiRssHubRoute { Route = "/test" } } }
        };

        var result = GeminiRoutePlanValidator.Validate(plan);
        Assert.True(result.Success);
    }

    [Fact]
    public void Validate_RejectsMissingRoute()
    {
        var plan = new GeminiRoutePlan
        {
            Mode = "single",
            Items = { new GeminiRouteItem { Title = "Test" } }
        };

        var result = GeminiRoutePlanValidator.Validate(plan);
        Assert.False(result.Success);
    }
}
