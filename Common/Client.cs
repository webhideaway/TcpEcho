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

        private async Task PostAsync(Task task, int? callbackPort = null, Action<ReadOnlyMemory<byte>> handler = null)
        {
            InitCallback(callbackPort);

            await Task.WhenAll(task,
                _callbackServer.Value == null ? Task.CompletedTask : _callbackServer.Value.ListenAsync(handler)
            );
        }

        public async Task PostAsync(Stream stream, int? callbackPort = null, Action<ReadOnlyMemory<byte>> handler = null)
        {
            await PostAsync(stream.CopyToAsync(_clientStream), callbackPort, handler);
        }

        public async Task PostAsync(byte[] data, int? callbackPort = null, Action<ReadOnlyMemory<byte>> handler = null)
        {
            await PostAsync(_clientStream.WriteAsync(data, 0, data.Length), callbackPort, handler);
        }
    }
}
