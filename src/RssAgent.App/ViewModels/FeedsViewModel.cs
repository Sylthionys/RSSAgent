using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RssAgent.Automation;
using RssAgent.Core.Logging;
using RssAgent.Core.Models;
using RssAgent.Integrations.RssBox;

namespace RssAgent.App.ViewModels;

public partial class FeedsViewModel : ObservableObject
{
    private readonly SettingsViewModel _settingsViewModel;
    private readonly RssBoxClient _rssBoxClient;
    private readonly AppLogger _logger;

    public ObservableCollection<FeedInfo> Feeds { get; } = new();
    public ObservableCollection<TagInfo> Tags { get; } = new();
    public ObservableCollection<DigestInfo> Digests { get; } = new();

    public event Action<IReadOnlyList<TagInfo>>? TagsUpdated;

    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isBusy;

    public FeedsViewModel(SettingsViewModel settingsViewModel, RssBoxClient rssBoxClient, AppLogger logger)
    {
        _settingsViewModel = settingsViewModel;
        _rssBoxClient = rssBoxClient;
        _logger = logger;
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        var settings = _settingsViewModel.BuildUserSettings();
        var session = new RssBoxSessionSettings
        {
            BaseUrl = settings.RssBoxBaseUrl,
            Username = settings.RssBoxUsername,
            Password = settings.RssBoxPassword,
            Headless = true
        };

        IsBusy = true;
        StatusMessage = "刷新 RSSBox 数据...";
        try
        {
            var snapshot = await _rssBoxClient.GetSnapshotAsync(session);

            Feeds.Clear();
            foreach (var feed in snapshot.Feeds)
            {
                Feeds.Add(feed);
            }

            Tags.Clear();
            foreach (var tag in snapshot.Tags)
            {
                Tags.Add(tag);
            }

            TagsUpdated?.Invoke(snapshot.Tags);

            Digests.Clear();
            foreach (var digest in snapshot.Digests)
            {
                Digests.Add(digest);
            }

            _settingsViewModel.ApplyRssBoxOptions(snapshot.Options);
            StatusMessage = $"Feeds: {Feeds.Count}, Tags: {Tags.Count}, Digests: {Digests.Count}";
        }
        catch (RssBoxAutomationException ex)
        {
            StatusMessage = BuildAutomationError("Refresh RSSBox failed", ex);
            await _logger.ErrorAsync("Refresh RSSBox failed.", ex.Message);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Refresh failed: {ex.Message}";
            await _logger.ErrorAsync("Refresh RSSBox failed.", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static string BuildAutomationError(string prefix, RssBoxAutomationException ex)
    {
        if (!string.IsNullOrWhiteSpace(ex.ScreenshotPath))
        {
            return $"{prefix}: {ex.Message}. Screenshot: {ex.ScreenshotPath}";
        }

        return $"{prefix}: {ex.Message}";
    }
}
