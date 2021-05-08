using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using ZeroFormatter;
using ZeroFormatter.Internal;

namespace Common
{
    public class Server : Processor
    {
        private readonly Socket _listenSocket;
        private readonly IFormatter _formatter;

        private readonly static ConcurrentDictionary<Type, Delegate> _registeredHandlers
            = new ConcurrentDictionary<Type, Delegate>();

        public Server(IPEndPoint listenEndPoint, IFormatter formatter = null) : base()
        {
            _listenSocket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            _listenSocket.Bind(listenEndPoint);

            _listenSocket.Listen(120);
            _formatter = formatter ?? new DefaultFormatter();
        }

        public void RegisterHandler<TRequest, TResponse>(Func<TRequest, TResponse> handler)
        {
            _registeredHandlers.AddOrUpdate(typeof(TRequest), handler, (k, v) =>
            {
                return v.GetInvocationList().Contains(handler)
                    ? v : Delegate.Combine(v, handler);
            });
        }

        public void RegisterHandler<TData>(Action<TData> handler)
        {
            _registeredHandlers.AddOrUpdate(typeof(TData), handler, (k, v) =>
            {
                return v.GetInvocationList().Contains(handler)
                    ? v : Delegate.Combine(v, handler);
            });
        }

        private async Task<PipeReader> AcceptAsync()
        {
            var socket = await _listenSocket.AcceptAsync();
            var stream = new NetworkStream(socket);
            return PipeReader.Create(stream, new StreamPipeReaderOptions(leaveOpen: true));
        }

        public async Task ListenAsync()
        {
            while (true)
            {
                var reader = await AcceptAsync();
                await ProcessMessagesAsync(reader);
            }
        }

        public async Task ListenAsync(Action<object> handler)
        {
            var reader = await AcceptAsync();
            _ = ProcessMessagesAsync(reader, message => {
                var type = Type.GetType(message.RequestType);
                handler(_formatter.Deserialize(type, message.RawData));
            });
        }

        public override PipeWriter GetWriter(Message message)
        {
            IPEndPoint callbackEndPoint = null;

            if (message.CallbackAddress != null && message.CallbackPort > 0)
            {
                var callbackAddress = new IPAddress(message.CallbackAddress);
                callbackEndPoint = new IPEndPoint(callbackAddress, message.CallbackPort);
            }

            if (callbackEndPoint == null) return default;

            var callbackSocket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            callbackSocket.Connect(callbackEndPoint);

            var callbackStream = new NetworkStream(callbackSocket);
            return PipeWriter.Create(callbackStream, new StreamPipeWriterOptions(leaveOpen: true));
        }

        protected override bool TryReadMessage(ref ReadOnlySequence<byte> buffer, out Message message)
        {
            SequencePosition? eomPos = buffer.PositionOf(Message.EOM);
            if (eomPos == null)
            {
                message = default;
                return false;
            }

            var raw = buffer.Slice(0, eomPos.Value).ToArray();
            message = ZeroFormatterSerializer.Deserialize<Message>(raw);

            buffer = new ReadOnlySequence<byte>(
                buffer.Slice(raw.Length + 1).ToArray().
                    SkipWhile(bit => bit == Convert.ToByte(null)).ToArray());
            return true;
        }

        protected override async Task<Message[]> ProcessMessageAsync(Message message)
        {
            var type = Type.GetType(message.RequestType);
            if (_registeredHandlers.TryGetValue(type, out Delegate handlers))
            {
                var request = _formatter.Deserialize(type, message.RawData);
                return await Task.WhenAll(handlers.GetInvocationList().Select(handler =>
                    Task.Factory.StartNew(() =>
                    {
                        object response = null;
                        try
                        {
                            response = handler.DynamicInvoke(request);
                        }
                        catch (Exception ex)
                        {
                            response = ex.GetBaseException();
                        }
                        var type = response.GetType();
                        var raw = _formatter.Serialize(type, response);
                        return Message.Create(type, raw);
                    })
                ));
            }
            return default;
        }

        protected override async IAsyncEnumerable<FlushResult> WriteMessagesAsync(PipeWriter writer, params Message[] messages)
        {
            foreach (var message in messages)
            {
                var data = ZeroFormatterSerializer.Serialize<Message>(message);
                BinaryUtil.WriteByte(ref data, data.Length, Message.EOM);
                yield return await writer.WriteAsync(data);
            }
        }
    }
}
