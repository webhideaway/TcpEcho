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
            return PipeReader.Create(stream, new StreamPipeReaderOptions(leaveOpen: true));
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
                    messages => {
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

            return PipeWriter.Create(callbackStream, new StreamPipeWriterOptions(leaveOpen: true));
        }

        protected override bool TryReadMessage(ReadOnlySequence<byte> buffer, ref int position, out Message message)
        {
            var start = position;
            var end = buffer.PositionOf(Message.EOM);

            message = default;
            if (end == null)
                return false;

            var consumed = buffer.Slice(start, end.Value).ToArray();
            message = ZeroFormatterSerializer.Deserialize<Message>(consumed);

            position = start + consumed.Length + 1;
            return true;
        }

        protected override async Task<Message[]> ProcessMessageAsync(Message message)
        {
            var type = Type.GetType(message.TypeName);
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

        protected override async IAsyncEnumerable<FlushResult> WriteMessagesAsync(Message input, params Message[] outputs)
        {
            var writer = GetWriter(input);
            if (writer == null) yield return default;
            foreach (var output in outputs ?? new Message[] { })
            {
                var data = ZeroFormatterSerializer.Serialize<Message>(output);
                BinaryUtil.WriteByte(ref data, data.Length, Message.EOM);
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
