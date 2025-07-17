using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using EmitterHub.ArtNet;
using EmitterHub.eHub;
using EmitterHub.Routing;
using EmitterHub.UI.ViewModels;

namespace EmitterHub.UI;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // 1) Instancie les composants métier (comme dans ton Main console)
        var receiver = new EHubReceiver(port: 8765, targetUniverse: 1);
        var sender = new ArtNetSender();
        var router = new Router(receiver, sender);

        // Charge ton CSV
        CsvMappingLoader.Load("EmitterHub/Config/mapping_clean.csv", router);

        // Démarre le routage asynchrone
        _ = router.StartAsync();

        // 2) Crée le ViewModel en lui passant ces instances
        var statsVm = new StatsViewModel(router, receiver, sender);

        // 3) Lie le DataContext de la fenêtre principale
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = statsVm
            };
        }
        base.OnFrameworkInitializationCompleted();
    }
}