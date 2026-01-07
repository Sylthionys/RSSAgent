using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using RssAgent.App.ViewModels;
using RssAgent.Core.Logging;
using RssAgent.Core.Security;
using RssAgent.Core.Services;
using RssAgent.Integrations.RssBox;
using RssAgent.Integrations.RssHub;

namespace RssAgent.App;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        var mainWindow = new MainWindow
        {
            DataContext = _serviceProvider.GetRequiredService<MainViewModel>()
        };
        mainWindow.Show();

        _ = ((MainViewModel)mainWindow.DataContext).InitializeAsync();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<AppLogger>();
        services.AddSingleton<DpapiProtector>();
        services.AddSingleton<SettingsService>();
        services.AddSingleton<RssHubClient>();
        services.AddSingleton<RssBoxClient>();

        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<FeedsViewModel>();
        services.AddSingleton<AddSubscriptionViewModel>();
        services.AddSingleton<RecommendViewModel>();
        services.AddSingleton<DigestsViewModel>();
        services.AddSingleton<LogsViewModel>();
        services.AddSingleton<MainViewModel>();
    }
}
