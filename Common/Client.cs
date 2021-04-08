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
        private Lazy<Server> _callbackServer;
        public Client(int remotePort)
        {
            var clientSocket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            Console.WriteLine($"Connecting to remote port {remotePort}");

            clientSocket.Connect(new IPEndPoint(IPAddress.Loopback, remotePort));
            _clientStream = new NetworkStream(clientSocket);
        }
        public async Task PostAsync(int? callbackPort = null)
        {
            _callbackServer = new Lazy<Server>(() => 
                callbackPort.HasValue ? new Server(callbackPort.Value) : null);

            await Task.WhenAll(
                Console.OpenStandardInput().CopyToAsync(_clientStream),
                _callbackServer.Value == null ? Task.CompletedTask : _callbackServer.Value.ListenAsync()
            ); ;
        }
    }
}
