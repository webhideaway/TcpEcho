using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using ZeroFormatter;

namespace Common
{
    public class Server : Processor
    {
        private readonly Socket _listenSocket;
        private readonly IFormatter _formatter;

        private readonly static ConcurrentDictionary<IPEndPoint, PipeWriter> _pipeWriters
            = new ConcurrentDictionary<IPEndPoint, PipeWriter>();
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

        public async Task ListenAsync()
        {
            while (true)
            {
                var socket = await _listenSocket.AcceptAsync();
                var stream = new NetworkStream(socket);
                var reader = PipeReader.Create(stream);

                await ProcessMessagesAsync(reader, GetWriter);
            }
        }

        private Func<Message, PipeWriter> GetWriter = message =>
        {
            IPEndPoint callbackEndPoint = null;

            if (message.CallbackAddress != null && message.CallbackPort > 0)
            {
                var callbackAddress = new IPAddress(message.CallbackAddress);
                callbackEndPoint = new IPEndPoint(callbackAddress, message.CallbackPort);
            }

            if (callbackEndPoint == null) return default;

            return _pipeWriters.GetOrAdd(callbackEndPoint, callbackEndPoint =>
            {
                var callbackSocket = new Socket(SocketType.Stream, ProtocolType.Tcp);
                callbackSocket.Connect(callbackEndPoint);

                var callbackStream = new NetworkStream(callbackSocket);
                return PipeWriter.Create(callbackStream, new StreamPipeWriterOptions(leaveOpen: true));
            });
        };

        protected override bool TryReadMessage(ref ReadOnlySequence<byte> buffer, out Message message)
        {
            SequencePosition? eomPos = buffer.PositionOf(Convert.ToByte(ConsoleKey.Escape));
            if (eomPos == null)
            {
                message = default;
                return false;
            }

            var raw = buffer.Slice(0, eomPos.Value).ToArray();
            message = ZeroFormatterSerializer.Deserialize<Message>(raw);

            buffer = buffer.Slice(raw.Length + 1);
            return true;
        }

        protected override async Task<FlushResult[]> ProcessMessageAsync(PipeWriter writer, Message message)
        {
            var type = Type.GetType(message.RequestType);
            if (_registeredHandlers.TryGetValue(type, out Delegate handlers))
            {
                var request = _formatter.Deserialize(type, message.RawData);
                await Task.WhenAll(handlers.GetInvocationList().Select(handler =>
                    {
                        var task = Task.Factory.StartNew(() => handler.DynamicInvoke(request), TaskCreationOptions.LongRunning);
                        var success = task.ContinueWith(t => ProcessResponse(writer, t.Result), TaskContinuationOptions.OnlyOnRanToCompletion).Unwrap();
                        var failure = task.ContinueWith(t => ProcessResponse(writer, t.Exception?.Flatten()?.GetBaseException()), TaskContinuationOptions.NotOnRanToCompletion).Unwrap();
                        return Task.WhenAny(success, failure).Unwrap();
                    }
                ));
            }
            return default;
        }

        private Task<FlushResult> ProcessResponse(PipeWriter writer, object response)
        {
            if (writer == null) return default;
            if (response == null) return default;
            var type = response.GetType();
            var raw = _formatter.Serialize(type, response);
            var message = Message.Create(type, raw);
            var output = ZeroFormatterSerializer.Serialize<Message>(message);
            return writer.WriteAsync(output).AsTask();
        }
    }
}
