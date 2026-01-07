using System.Collections.Generic;
using RssAgent.Core.Services;
using Xunit;

namespace RssAgent.Tests;

public class RssHubUrlBuilderTests
{
    [Fact]
    public void Build_AppendsQuery()
    {
        var url = RssHubUrlBuilder.Build(
            "http://localhost:21200",
            "/test/route",
            new Dictionary<string, string> { ["a"] = "b" });

        Assert.Contains("a=b", url);
    }

    [Fact]
    public void Build_MergesExistingQuery()
    {
        var url = RssHubUrlBuilder.Build(
            "http://localhost:21200",
            "/test/route?x=1",
            new Dictionary<string, string> { ["y"] = "2" });

        Assert.Contains("x=1", url);
        Assert.Contains("y=2", url);
    }
}
