using System;
using System.IO;

namespace RssAgent.Core;

public static class AppPaths
{
    public static string AppDataRoot =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RssAgent");

    public static string SettingsFilePath => Path.Combine(AppDataRoot, "appsettings.json");

    public static string LogsDirectory => Path.Combine(AppDataRoot, "logs");

    public static string ScreenshotsDirectory => Path.Combine(LogsDirectory, "screenshots");

    public static string TracesDirectory => Path.Combine(LogsDirectory, "traces");
}
