using CommunityToolkit.Mvvm.ComponentModel;

namespace RssAgent.App.ViewModels;

public partial class TagOptionViewModel : ObservableObject
{
    public TagOptionViewModel(string name, string? id = null)
    {
        Name = name;
        Id = id;
    }

    public string Name { get; }

    public string? Id { get; }

    [ObservableProperty] private bool _isSelected;
}
