using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RssAgent.Core.Logging;

public sealed class AppLogger
{
    private readonly SemaphoreSlim _mutex = new(1, 1);

    public event Action<string>? LogReceived;

    public async Task LogAsync(string level, string message, string? detail = null)
    {
        Directory.CreateDirectory(AppPaths.LogsDirectory);
        var timestamp = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss");
        var line = $"[{timestamp}] [{level}] {message}";
        if (!string.IsNullOrWhiteSpace(detail))
        {
            line += $" | {detail}";
        }

        await _mutex.WaitAsync();
        try
        {
            await File.AppendAllTextAsync(Path.Combine(AppPaths.LogsDirectory, "app.log"), line + Environment.NewLine, Encoding.UTF8);
        }
        finally
        {
            _mutex.Release();
        }

        LogReceived?.Invoke(line);
    }

    public Task InfoAsync(string message, string? detail = null) => LogAsync("INFO", message, detail);

    public Task WarnAsync(string message, string? detail = null) => LogAsync("WARN", message, detail);

    public Task ErrorAsync(string message, string? detail = null) => LogAsync("ERROR", message, detail);
}
