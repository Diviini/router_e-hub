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
        int listenPort = 8765;  // Port ArtNet standard
        int eHubUniverse = 1;   // Univers eHuB par défaut

        // Initialisation des composants
        var receiver = new EHubReceiver(listenPort, eHubUniverse);
        var sender = new ArtNetSender();
        var router = new Router(receiver, sender);

        // Configuration d'exemple pour l'écran LED
        CsvMappingLoader.Load("EmitterHub/Config/mapping.csv", router);

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

}