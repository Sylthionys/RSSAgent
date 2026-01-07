using System;

namespace RssAgent.Automation;

public sealed class RssBoxAutomationException : Exception
{
    public RssBoxAutomationException(string stage, string message, string? screenshotPath, Exception? inner = null)
        : base(message, inner)
    {
        Stage = stage;
        ScreenshotPath = screenshotPath;
    }

    public string Stage { get; }
    public string? ScreenshotPath { get; }
}
