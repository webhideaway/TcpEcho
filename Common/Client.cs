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

        private Task PostTask(Message message)
        {
            var data = ZeroFormatterSerializer.Serialize<Message>(message);
            BinaryUtil.WriteByte(ref data, data.Length, Convert.ToByte(ConsoleKey.Escape));

            return _remoteWriter.WriteAsync(data).AsTask();
        }

        private Task ListenTask<TData>(Action<TData> handler)
        {
            if (handler == null) return Task.CompletedTask;

            _callbackListener.Value.RegisterHandler(handler);
            return _callbackListener.Value.ListenAsync();
        }

        private async Task PostAsync<TData>(Message message, Action<TData> handler = null)
        {
            await Task.WhenAll(
                PostTask(message),
                ListenTask<TData>(handler));
        }

        public async Task PostAsync<TData>(TData data)
        {
            var raw = _formatter.Serialize<TData>(data);
            var message = Message.Create<TData>(raw);
            await PostAsync<TData>(message);
        }

        public async Task PostAsync<TRequest, TResponse>(TRequest request, Action<TResponse> handler)
        {
            var raw = _formatter.Serialize<TRequest>(request);
            var input = Message.Create<TRequest>(raw, _callbackEndPoint);
            await PostAsync<Message>(input, output => 
                handler(_formatter.Deserialize<TResponse>(output.RawData)));
        }
    }
}
