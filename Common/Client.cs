using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Common
{
    public class Client
    {
        private readonly Stream _remoteStream;
        private Lazy<Server> _callbackListener;

        private readonly EndPoint _callbackEndPoint;
        private readonly IFormatter _formatter;

        public Client(EndPoint remoteEndPoint, EndPoint callbackEndPoint = null, IFormatter formatter = null)
        {
            var remoteSocket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            Console.WriteLine($"Connecting to remote end point {remoteEndPoint}");

            remoteSocket.Connect(remoteEndPoint);
            _remoteStream = new NetworkStream(remoteSocket);
            
            _callbackEndPoint = callbackEndPoint;
            _formatter = formatter ?? new DefaultFormatter();
        }

        private void InitCallback()
        {
            _callbackListener = new Lazy<Server>(() =>
               _callbackEndPoint == null ? null : new Server(_callbackEndPoint));
        }

        private Task PostTask(Stream stream)
        {
            return stream.CopyToAsync(_remoteStream);
        }

        private Task PostTask(byte[] data)
        {
            return _remoteStream.WriteAsync(data, 0, data.Length);
        }

        private Task CallbackTask(Action<ReadOnlyMemory<byte>> handler)
        {
            return handler == null || _callbackListener.Value == null 
                ? Task.CompletedTask : _callbackListener.Value.ListenAsync(handler);
        }

        private async Task PostAsync(Task task, Action<ReadOnlyMemory<byte>> handler = null)
        {
            InitCallback();

            await Task.WhenAll(task, CallbackTask(handler));
        }

        public async Task PostAsync(Stream stream, Action<ReadOnlyMemory<byte>> handler = null)
        {
            await PostAsync(PostTask(stream), handler);
        }

        public async Task PostAsync(byte[] data, Action<ReadOnlyMemory<byte>> handler = null)
        {
            await PostAsync(PostTask(data), handler);
        }

        public async Task PostAsync<TData>(TData data)
        {
            var input = _formatter.Serialize<TData>(data);

            await PostAsync(input);
        }

        public async Task<TResponse> PostAsync<TRequest, TResponse>(TRequest request)
        {
            var input = _formatter.Serialize<TRequest>(request);

            var response = default(TResponse);

            void handler(ReadOnlyMemory<byte> output)
            {
                response = _formatter.Deserialize<TResponse>(output.ToArray());
            }

            await PostAsync(input, handler);

            return response;
        }
    }
}
