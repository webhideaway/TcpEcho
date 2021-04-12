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
        public Client(EndPoint remoteEndPoint)
        {
            var clientSocket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            Console.WriteLine($"Connecting to remote end point {remoteEndPoint}");

            clientSocket.Connect(remoteEndPoint);
            _clientStream = new NetworkStream(clientSocket);
        }

        private async Task PostAsync(Task task, EndPoint callbackEndPoint = null, Action<ReadOnlyMemory<byte>> handler = null)
        {
            _callbackServer = new Lazy<Server>(() =>
                callbackEndPoint == null ? null : new Server(callbackEndPoint));

            await Task.WhenAll(task,
                _callbackServer.Value == null ? Task.CompletedTask : _callbackServer.Value.ListenAsync(handler)
            );
        }

        public async Task PostAsync(Stream stream, EndPoint callbackEndPoint = null, Action<ReadOnlyMemory<byte>> handler = null)
        {
            await PostAsync(stream.CopyToAsync(_clientStream), callbackEndPoint, handler);
        }

        public async Task PostAsync(byte[] data, EndPoint callbackEndPoint = null, Action<ReadOnlyMemory<byte>> handler = null)
        {
            await PostAsync(_clientStream.WriteAsync(data, 0, data.Length), callbackEndPoint, handler);
        }

        public async Task<TResponse> PostAsync<TRequest, TResponse>(TRequest request)
        {
            return await Task.FromResult<TResponse>(default);
        }
    }
}
