using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Common
{
    public class Client
    {
        public static async Task RunAsync()
        {
            var clientSocket = new Socket(SocketType.Stream, ProtocolType.Tcp);

            Console.WriteLine("Connecting to port 8087");

            clientSocket.Connect(new IPEndPoint(IPAddress.Loopback, 8087));
            var stream = new NetworkStream(clientSocket);

            await Console.OpenStandardInput().CopyToAsync(stream);
        }
    }
}
