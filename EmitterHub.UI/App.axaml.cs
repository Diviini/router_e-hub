using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
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

    // ---- Windows-only P/Invoke. Declaring it is fine cross-platform; just never call it outside Windows. ----
    [SupportedOSPlatform("windows")]
    [DllImport("kernel32.dll", SetLastError = false)]
    private static extern bool AllocConsole();
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (OperatingSystem.IsWindows())
        {
            try { AllocConsole(); } catch {}
        }
        // 1) Instancie les composants métier (comme dans ton Main console)
        var receiver = new EHubReceiver(port: 8765, targetUniverse: 1);
        var sender = new ArtNetSender();
        var router = new Router(receiver, sender);

        // Charge ton CSV
        var configDir = Path.Combine(AppContext.BaseDirectory, "Config");
        var mappingCsv = Path.Combine(configDir, "mapping_clean.csv");
        CsvMappingLoader.Load(mappingCsv, router);

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