using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace RssAgent.App.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    public SettingsViewModel Settings { get; }
    public FeedsViewModel Feeds { get; }
    public AddSubscriptionViewModel AddSubscription { get; }
    public RecommendViewModel Recommend { get; }
    public DigestsViewModel Digests { get; }
    public LogsViewModel Logs { get; }

    [ObservableProperty] private string _chatInput = string.Empty;

    public MainViewModel(
        SettingsViewModel settings,
        FeedsViewModel feeds,
        AddSubscriptionViewModel addSubscription,
        RecommendViewModel recommend,
        DigestsViewModel digests,
        LogsViewModel logs)
    {
        Settings = settings;
        Feeds = feeds;
        AddSubscription = addSubscription;
        Recommend = recommend;
        Digests = digests;
        Logs = logs;

        Feeds.TagsUpdated += tags => AddSubscription.UpdateAvailableTags(tags);
    }

    public async Task InitializeAsync()
    {
        await Settings.LoadAsync();
        await Settings.RunStartupChecksAsync();

        await Feeds.RefreshAsync();

        await Digests.RefreshAsync();
    }

    [RelayCommand]
    private void GenerateAndAddFromChat()
    {
        AddSubscription.InputText = ChatInput;
        if (AddSubscription.GenerateAndAddCommand.CanExecute(null))
        {
            AddSubscription.GenerateAndAddCommand.Execute(null);
        }
    }

    [RelayCommand]
    private void RecommendFromChat()
    {
        Recommend.Topic = ChatInput;
        if (Recommend.RecommendCommand.CanExecute(null))
        {
            Recommend.RecommendCommand.Execute(null);
        }
    }
}
