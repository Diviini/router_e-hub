namespace EmitterHub.ArtNet;

using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

public class ArtNetSender
{
    public static void SendTestPacket()
    {


        // Configuration
        string controllerIp = "192.168.1.45";
        int universe = 0;
        int port = 6454;

        // Données DMX (512 canaux)
        byte[] dmxData = new byte[512];
        dmxData[0] = 255; // Rouge
        dmxData[1] = 0;   // Vert
        dmxData[2] = 0;   // Bleu

        // Construction du paquet ArtNet
        byte[] packet = new byte[18 + dmxData.Length];
        int index = 0;

        // Header "Art-Net" + null terminator
        Encoding.ASCII.GetBytes("Art-Net\0").CopyTo(packet, index);
        index += 8;

        // OpCode DMX (0x5000), little-endian
        packet[index++] = 0x00;
        packet[index++] = 0x50;

        // Protocol version (high byte first = big-endian)
        packet[index++] = 0x00;
        packet[index++] = 0x0e; // version 14 par exemple

        // Sequence
        packet[index++] = 0x00;

        // Physical
        packet[index++] = 0x00;

        // Universe (little-endian)
        packet[index++] = (byte)(universe & 0xFF);
        packet[index++] = (byte)((universe >> 8) & 0xFF);

        // Length (big-endian)
        packet[index++] = (byte)((dmxData.Length >> 8) & 0xFF);
        packet[index++] = (byte)(dmxData.Length & 0xFF);

        // DMX data
        dmxData.CopyTo(packet, index);

        // Envoi UDP
        using (UdpClient udpClient = new UdpClient())
        {
            udpClient.Send(packet, packet.Length, controllerIp, port);
        }

        Console.WriteLine("Paquet ArtNet envoyé.");
    }
}
