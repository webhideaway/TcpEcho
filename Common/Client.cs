using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Common
{
    public class Client
    {
        private readonly Stream _clientStream;
        private readonly Server _callbackServer;
        public Client(int remotePort, int? callbackPort = null)
        {
            var clientSocket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            Console.WriteLine($"Connecting to remote port {remotePort}");

            clientSocket.Connect(new IPEndPoint(IPAddress.Loopback, remotePort));
            _clientStream = new NetworkStream(clientSocket);

            _callbackServer = callbackPort.HasValue ? new Server(callbackPort.Value) : null;
        }
        public async Task PostAsync()
        {
            await Task.WhenAll(
                Console.OpenStandardInput().CopyToAsync(_clientStream),
                _callbackServer == null ? Task.CompletedTask : _callbackServer.ListenAsync()
            ); ;
        }
    }
}
