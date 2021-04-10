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

        private void InitCallback(int? callbackPort = null)
        {
            _callbackServer = new Lazy<Server>(() =>
                callbackPort.HasValue ? new Server(callbackPort.Value) : null);
        }

        public async Task PostAsync(Stream stream, int? callbackPort = null, Action<byte[]> handler = null)
        {
            InitCallback(callbackPort);

            await Task.WhenAll(
                stream.CopyToAsync(_clientStream),
                _callbackServer.Value == null ? Task.CompletedTask : _callbackServer.Value.ListenAsync(handler)
            ); ;
        }

        public async Task PostAsync(byte[] data, int? callbackPort = null, Action<byte[]> handler = null)
        {
            InitCallback(callbackPort);

            await Task.WhenAll(
                _clientStream.WriteAsync(data, 0, data.Length),
                _callbackServer.Value == null ? Task.CompletedTask : _callbackServer.Value.ListenAsync(handler)
            ); ;
        }
    }
}
