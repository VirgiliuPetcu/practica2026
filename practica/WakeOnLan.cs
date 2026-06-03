using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;


namespace Practica
{
    public static class WakeOnLan
    {

        private static UdpClient udpClient = new UdpClient();
        
        
        public static void  SendWakePacket(string MAC_ADDRESS) {
            udpClient.EnableBroadcast = true;
            
            MemoryStream stream = new MemoryStream();
            BinaryWriter writer = new BinaryWriter(stream);

            string [] bytes = MAC_ADDRESS.Split(':');
            byte [] adressBytes = bytes.Select(x => Convert.ToByte(x, 16)).ToArray();

            writer.Write(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF });
            for (int i = 0; i < 16; i++) {
                writer.Write(adressBytes);
            }

            byte[] packet = stream.ToArray();
            
            udpClient.Send(packet,packet.Length, new IPEndPoint(IPAddress.Broadcast, 9));

        }


      


    }
}
