using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RssAgent.Automation;
using RssAgent.Core.Logging;
using RssAgent.Core.Models;
using RssAgent.Integrations.RssBox;

namespace RssAgent.App.ViewModels;

public partial class DigestsViewModel : ObservableObject
{
    private readonly SettingsViewModel _settingsViewModel;
    private readonly RssBoxClient _rssBoxClient;
    private readonly AppLogger _logger;

    public ObservableCollection<DigestInfo> Digests { get; } = new();

    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isBusy;

    public DigestsViewModel(SettingsViewModel settingsViewModel, RssBoxClient rssBoxClient, AppLogger logger)
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
        try
        {
            var snapshot = await _rssBoxClient.GetSnapshotAsync(session);
            Digests.Clear();
            foreach (var digest in snapshot.Digests)
            {
                Digests.Add(digest);
            }

            StatusMessage = $"共 {Digests.Count} 个 Digest。";
        }
        catch (RssBoxAutomationException ex)
        {
            StatusMessage = BuildAutomationError("Refresh digests failed", ex);
            await _logger.ErrorAsync("Refresh digests failed.", ex.Message);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Refresh failed: {ex.Message}";
            await _logger.ErrorAsync("Refresh digests failed.", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void OpenDigestPage()
    {
        var url = _settingsViewModel.RssBoxBaseUrl.TrimEnd('/') + "/core/digest/";
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    [RelayCommand]
    private void CreateDigest()
    {
        StatusMessage = "TODO: RSSBox Digest 创建流程需根据 UI 结构补齐。";
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
