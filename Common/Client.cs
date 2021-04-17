using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using ZeroFormatter.Internal;

namespace Common
{
    public class Client
    {
        private readonly Stream _remoteStream;
        private Lazy<Server> _callbackListener;

        private readonly IPEndPoint _callbackEndPoint;
        private readonly IFormatter _formatter;

        public Client(IPEndPoint remoteEndPoint, IPEndPoint callbackEndPoint = null, IFormatter formatter = null)
        {
            var remoteSocket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            remoteSocket.Connect(remoteEndPoint);

            _remoteStream = new BufferedStream(new NetworkStream(remoteSocket));

            _callbackEndPoint = callbackEndPoint;
            _formatter = formatter ?? new DefaultFormatter();
        }

        private Task PostTask(Message message)
        {
            var data = _formatter.Serialize<Message>(message);
            BinaryUtil.WriteByte(ref data, data.Length, Convert.ToByte(ConsoleKey.Escape));

            return _remoteStream.WriteAsync(data, 0, data.Length);
        }

        private Task CallbackTask(Action<Message> handler)
        {
            if (handler == null) return Task.CompletedTask;

            _callbackListener = new Lazy<Server>(
                () => new Server(_callbackEndPoint));

            _callbackListener.Value.RegisterHandler(handler);
            return _callbackListener.Value.ListenAsync();
        }

        private async Task PostAsync(Message message, Action<Message> handler = null)
        {
            await Task.WhenAll(
                PostTask(message),
                CallbackTask(handler));
        }

        public async Task PostAsync<TData>(TData data)
        {
            var raw = _formatter.Serialize<TData>(data);
            var message = Message.Create<TData>(raw);
            await PostAsync(message);
        }

        public async Task<TResponse> PostAsync<TRequest, TResponse>(TRequest request)
        {
            var raw = _formatter.Serialize<TRequest>(request);
            var input = Message.Create<TRequest>(raw);

            var response = default(TResponse);
            void handler(Message output)
            {
                response = _formatter.Deserialize<TResponse>(output.Raw);
            }

            await PostAsync(input, handler);
            return response;
        }
    }
}
