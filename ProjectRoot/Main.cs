using EmitterHub.ArtNet;

class MainApp
{
    static void Main(string[] args)
    {
        Console.WriteLine("Démarrage...");
        ArtNetSender.SendTestPacket();
    }
}
