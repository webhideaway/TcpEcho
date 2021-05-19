using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using ZeroFormatter;
using ZeroFormatter.Internal;
using ZeroPipeline.Interfaces;

namespace ZeroPipeline
{
    public class Server : Processor, IDisposable
    {
        private readonly Socket _listenSocket;
        private readonly IFormatter _formatter;
        private bool _disposedValue;

        private readonly ConcurrentDictionary<Type, Delegate> _registeredHandlers
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
            var reader = new StreamPipeReaderOptions(leaveOpen: true);
            return PipeReader.Create(stream, reader);
        }

        private void Handle(Message message, Action<object> handler)
        {
            if (handler == null) return;
            var type = Type.GetType(message.TypeName);
            var data = _formatter.Deserialize(type, message.RawData);
            handler(data);
        }

        public async Task ListenAsync(
            Action<object> input = null, Action<object> output = null)
        {
            while (true)
            {
                var reader = await AcceptAsync();
                await ProcessMessagesAsync(reader,
                    message => Handle(message, input),
                    messages =>
                    {
                        foreach (var message in messages ?? new Message[] { })
                            Handle(message, output);
                    }
                );
            }
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
            callbackSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);

            callbackSocket.Connect(callbackEndPoint);
            var callbackStream = new NetworkStream(callbackSocket);

            var callbackWriter = new StreamPipeWriterOptions(leaveOpen: true);
            return PipeWriter.Create(callbackStream, callbackWriter);
        }

        protected override bool TryReadMessage(ref ReadOnlySequence<byte> buffer, out Message message)
        {
            message = default;
            var span = buffer.ToArray().AsSpan();

            var bomPos = span.IndexOf(Message.BOM);
            if (bomPos == -1) return false;

            var eomPos = span.IndexOf(Message.EOM);
            if (eomPos == -1) return false;

            var start = bomPos + Message.BOM.Length;
            var end = eomPos + Message.EOM.Length;
            var length = eomPos - start;

            var data = span.Slice(start, length).ToArray();
            message = ZeroFormatterSerializer.Deserialize<Message>(data);

            buffer = buffer.Slice(end);
            return true;
        }

        protected override async Task<Message[]> ProcessMessageAsync(Message message)
        {
            var type = Type.GetType(message.TypeName);
            if (_registeredHandlers.TryGetValue(type, out Delegate handlers))
            {
                return await Task.WhenAll(handlers.GetInvocationList().Select(handler =>
                    Task.Factory.StartNew(() =>
                    {
                        try
                        {
                            var type = Type.GetType(message.TypeName);
                            var request = _formatter.Deserialize(type, message.RawData);
                            var response = handler.DynamicInvoke(request);
                            var raw = _formatter.Serialize(type, response);
                            return Message.Create(type, raw);
                        }
                        catch (Exception ex)
                        {
                            var response = ex.GetBaseException();
                            var type = response.GetType();
                            var info = $"{response.Message}{Environment.NewLine}{response.StackTrace}";
                            var raw = _formatter.Serialize(type, info);
                            return Message.Create(type, raw);
                        }
                    })
                ));
            }
            return default;
        }

        protected override async IAsyncEnumerable<FlushResult> WriteMessagesAsync(Message input, params Message[] outputs)
        {
            var writer = GetWriter(input);
            if (writer == null) yield return default;
            foreach (var output in outputs ?? new Message[] { })
            {
                var data = new byte[] { };
                BinaryUtil.WriteBytes(ref data, 0, Message.BOM);
                ZeroFormatterSerializer.Serialize<Message>(ref data, Message.BOM.Length, output);
                BinaryUtil.WriteBytes(ref data, data.Length, Message.EOM);
                yield return await writer.WriteAsync(data);
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                    _listenSocket?.Dispose();
                    _formatter?.Dispose();
                    foreach (var registeredHandlerKey in _registeredHandlers.Keys)
                        _registeredHandlers[registeredHandlerKey] = null;
                    _registeredHandlers.Clear();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                _disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~Server()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
