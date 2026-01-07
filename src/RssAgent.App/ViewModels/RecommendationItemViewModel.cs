using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using RssAgent.Core.Models;

namespace RssAgent.App.ViewModels;

public partial class RecommendationItemViewModel : ObservableObject
{
    public RecommendationItemViewModel(GeminiRouteItem item)
    {
        Item = item;
        Title = item.Title;
        Notes = item.Notes ?? string.Empty;
        TagsDisplay = item.Tags.Count > 0 ? string.Join(", ", item.Tags) : string.Empty;
        RouteDisplay = item.DirectRssUrl ?? item.RssHub?.Route ?? string.Empty;
    }

    public GeminiRouteItem Item { get; }

    [ObservableProperty] private bool _isSelected = true;
    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string _notes = string.Empty;
    [ObservableProperty] private string _tagsDisplay = string.Empty;
    [ObservableProperty] private string _routeDisplay = string.Empty;
}
