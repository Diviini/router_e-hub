using EmitterHub.eHub;
using EmitterHub.Routing;
using EmitterHub.ArtNet;

namespace EmitterHub;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("EmitterHub - LED Installation Router");
        Console.WriteLine("====================================");

        // Configuration par défaut
        int listenPort = 6454;  // Port ArtNet standard
        int eHubUniverse = 0;   // Univers eHuB par défaut

        // Initialisation des composants
        var receiver = new EHubReceiver(listenPort, eHubUniverse);
        var sender = new ArtNetSender();
        var router = new Router(receiver, sender);

        // Configuration d'exemple pour l'écran LED
        SetupLedScreenRouting(router);

        // Démarrage du routage
        await router.StartAsync();

        // Boucle principale
        Console.WriteLine("Routage démarré. Appuyez sur 'q' pour quitter.");
        while (true)
        {
            var key = Console.ReadKey(true);
            if (key.KeyChar == 'q' || key.KeyChar == 'Q')
                break;

            if (key.KeyChar == 's')
            {
                Console.WriteLine("\n--- Statistiques ---");
                Console.WriteLine($"Messages eHuB reçus: {receiver.MessagesReceived}");
                Console.WriteLine($"Entités actives: {receiver.ActiveEntities}");
                Console.WriteLine($"Paquets ArtNet envoyés: {sender.PacketsSent}");
            }
        }

        // Arrêt propre
        await router.StopAsync();
        Console.WriteLine("Arrêt du routage.");
    }

    /// <summary>
    /// Configure le routage pour l'écran LED selon les spécifications
    /// </summary>
    private static void SetupLedScreenRouting(Router router)
    {
        // Configuration pour l'écran LED 128x128
        // Selon les spécifications du document

        // Premier quart - Contrôleur 192.168.1.45
        router.AddEntityRange(100, 4858, "192.168.1.45", 0, 31);

        // Deuxième quart - Contrôleur 192.168.1.46  
        router.AddEntityRange(5100, 9858, "192.168.1.46", 32, 63);

        // Troisième quart - Contrôleur 192.168.1.47
        router.AddEntityRange(10100, 14858, "192.168.1.47", 64, 95);

        // Quatrième quart - Contrôleur 192.168.1.48
        router.AddEntityRange(15100, 19858, "192.168.1.48", 96, 127);

        Console.WriteLine("Configuration écran LED chargée:");
        Console.WriteLine("- 4 contrôleurs configurés");
        Console.WriteLine("- 128 univers ArtNet");
        Console.WriteLine("- ~16,384 entités mappées");
    }
}