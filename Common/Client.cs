using System;
using System.IO;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using ZeroFormatter;
using ZeroFormatter.Internal;

namespace Common
{
    public class Client
    {
        private readonly PipeWriter _remoteWriter;
        private Lazy<Server> _callbackListener;

        private readonly IPEndPoint _callbackEndPoint;
        private readonly IFormatter _formatter;

        public Client(IPEndPoint remoteEndPoint, IPEndPoint callbackEndPoint = null, IFormatter formatter = null)
        {
            var remoteSocket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            remoteSocket.Connect(remoteEndPoint);

            var remoteStream = new NetworkStream(remoteSocket);
            _remoteWriter = PipeWriter.Create(remoteStream, new StreamPipeWriterOptions(leaveOpen: true));

            _callbackEndPoint = callbackEndPoint;
            _formatter = formatter ?? new DefaultFormatter();

            _callbackListener = new Lazy<Server>(
                () => new Server(_callbackEndPoint, formatter: _formatter));
        }

        private ReadOnlyMemory<byte> ProcessRequest<TRequest>(TRequest request)
        {
            var raw = _formatter.Serialize<TRequest>(request);
            var message = Message.Create<TRequest>(raw, _callbackEndPoint);
            var data = ZeroFormatterSerializer.Serialize<Message>(message);
            BinaryUtil.WriteByte(ref data, data.Length, Convert.ToByte(ConsoleKey.Escape));
            return data;
        }

        public async Task PostAsync<TRequest>(TRequest request)
        {
            var data = ProcessRequest<TRequest>(request);
            await _remoteWriter.WriteAsync(data).AsTask();
        }

        public async Task PostAsync<TRequest, TResponse>(TRequest request, Action<TResponse> handler)
        {
            var data = ProcessRequest<TRequest>(request);
            _callbackListener.Value.RegisterHandler(handler);
            await Task.WhenAll(
                _remoteWriter.WriteAsync(data).AsTask(),
                _callbackListener.Value.ListenAsync()
            );
        }
    }
}
