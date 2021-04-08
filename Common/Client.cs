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
        public static async Task RunAsync(int remotePort, int? callbackPort = null)
        {
            var clientSocket = new Socket(SocketType.Stream, ProtocolType.Tcp);

            Console.WriteLine($"Connecting to remote port {remotePort}");

            clientSocket.Connect(new IPEndPoint(IPAddress.Loopback, remotePort));
            var stream = new NetworkStream(clientSocket);

            await Task.WhenAll(
                Console.OpenStandardInput().CopyToAsync(stream),
                callbackPort.HasValue ? Common.Server.RunAsync(callbackPort.Value) : Task.CompletedTask
            );
        }
    }
}
