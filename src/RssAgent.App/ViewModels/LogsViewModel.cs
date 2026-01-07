using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using RssAgent.Core.Logging;

namespace RssAgent.App.ViewModels;

public sealed class LogsViewModel : ObservableObject
{
    public ObservableCollection<string> Lines { get; } = new();

    public LogsViewModel(AppLogger logger)
    {
        logger.LogReceived += line =>
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                Lines.Add(line);
                if (Lines.Count > 500)
                {
                    Lines.RemoveAt(0);
                }
            });
        };
    }
}
