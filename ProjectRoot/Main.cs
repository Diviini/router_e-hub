using EmitterHub.eHub;
using EmitterHub.Routing;
using EmitterHub.ArtNet;

namespace EmitterHub;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("EmitterHub - LED Installation Router (Sync Mode)");

        int listenPort = 8765;
        int eHubUniverse = 1;

        var receiver = new EHubReceiver(listenPort, eHubUniverse);
        var sender = new ArtNetSender();
        var router = new Router(receiver, sender);

        CsvMappingLoader.Load("EmitterHub/Config/mapping_clean.csv", router);

        Console.WriteLine("Boucle synchronisée démarrée. Appuyez sur 'q' pour quitter, 's' pour stats.");

        while (true)
        {
            var start = DateTime.Now;
            router.Tick();
            var elapsed = (DateTime.Now - start).TotalMilliseconds;
            int delay = Math.Max(0, 25 - (int)elapsed);
            Thread.Sleep(delay);

            if (Console.KeyAvailable)
            {
                var key = Console.ReadKey(true);
                if (key.KeyChar == 'q') break;
                if (key.KeyChar == 's')
                {
                    Console.WriteLine("--- Statistiques ---");
                    Console.WriteLine($"Messages eHuB reçus: {router.GetMessageCount()}");
                    Console.WriteLine($"Entités actives: {router.GetEntityCount()}");
                    Console.WriteLine($"Paquets ArtNet envoyés: {router.GetPacketsSent()}");
                    var map = router.GetIndexMap();
                    Console.WriteLine($"🔢 Mapping Index → Entity : {map.Count} entrées");
                    foreach (var pair in map.Take(10))
                        Console.WriteLine($"  Index {pair.Key} → Entity {pair.Value}");
                }
            }
        }

        Console.WriteLine("Arrêt du routage synchrone.");
    }
}
