using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.IO.Pipelines;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ZeroPipeline.Interfaces;

namespace ZeroPipeline
{
    public class Server : Processor, IDisposable
    {
        private readonly Socket _listenSocket;
        private readonly IFormatter _formatter;
        private bool _disposedValue;

        private readonly ConcurrentDictionary<Type, Delegate> _registeredHandlers = new();

        public Server(IPEndPoint listenEndPoint, IFormatter formatter = null) : base()
        {
            _listenSocket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            _listenSocket.Bind(listenEndPoint);

            _listenSocket.Listen(100); //this should be configurable
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

        private object ProcessMessage(Message message, out Type type)
        {
            type = Type.GetType(message.TypeName);
            object value;
            if (type.IsAssignableFrom(typeof(CancellationToken)))
                value = message.Id;
            else if (type.IsAssignableFrom(typeof(Exception)))
                value = Encoding.ASCII.GetString(message.RawData);
            else
                value = _formatter.Deserialize(type, message.RawData);
            return value;
        }

        public async Task ListenAsync(
            Action<Type, object> input = null,
            Action<Type, object, bool> output = null)
        {
            while (true)
            {
                var reader = await AcceptAsync();

                try
                {
                    while (true)
                    {
                        ReadResult readResult = await reader.ReadAsync();
                        ReadOnlySequence<byte> buffer = readResult.Buffer;

                        try
                        {
                            if (readResult.IsCanceled)
                                break;

                            while (TryReadMessage(ref buffer, out Message message))
                            {
                                _ = ProcessMessageAsync(message,
                                    message =>
                                    {
                                        var value = ProcessMessage(message, out Type type);
                                        input?.Invoke(type, value);
                                    },
                                    (message, done) =>
                                    {
                                        var value = ProcessMessage(message, out Type type);
                                        output?.Invoke(type, value, done);
                                    }
                                );
                            }

                            if (readResult.IsCompleted)
                                break;
                        }
                        finally
                        {
                            reader.AdvanceTo(buffer.Start, buffer.End);
                        }
                    }
                }
                finally
                {
                    await reader.CompleteAsync();
                }
            }
        }

        public async Task CallbackAsync(
            Action<Message> handler)
        {
            var reader = await AcceptAsync();

            try
            {
                ReadResult readResult = await reader.ReadAsync();
                ReadOnlySequence<byte> buffer = readResult.Buffer;

                try
                {
                    if (TryReadMessage(ref buffer, out Message message))
                        _ = ProcessMessageAsync(message,
                            message => handler?.Invoke(message)
                        );
                }
                finally
                {
                    reader.CancelPendingRead();
                }
            }
            finally
            {
                await reader.CompleteAsync();
            }
        }

        protected override async Task<Message[]> ProcessRequestAsync(Message request,
            CancellationToken cancellationToken)
        {
            var type = Type.GetType(request.TypeName);
            if (_registeredHandlers.TryGetValue(type, out Delegate handlers))
            {
                var invocationList = handlers.GetInvocationList();
                return await Task.WhenAll(invocationList.Select(handler =>
                {
                    var task = Task<Message>.Factory.StartNew(() =>
                    {
                        try
                        {
                            return HandleRequest(request, handler);
                        }
                        catch (Exception exception)
                        {
                            return HandleException(request.Id, exception.GetBaseException());
                        }
                    }, cancellationToken);

                    task.ContinueWith(task =>
                    {
                        return HandleException(request.Id, task.Exception?.Flatten()?.GetBaseException());
                    },
                    TaskContinuationOptions.NotOnRanToCompletion);

                    return task;
                }));
            }
            return new Message[] { };
        }
        private Message HandleRequest(Message request, Delegate handler)
        {
            var type = Type.GetType(request.TypeName);
            var input = _formatter.Deserialize(type, request.RawData);
            var output = handler.DynamicInvoke(input);
            var raw = _formatter.Serialize(type, output);
            return Message.Create(id: request.Id, type: type, rawData: raw);
        }

        private Message HandleException(string id, Exception exception)
        {
            var type = exception.GetType();
            var output = $"{exception.Message}";
            var raw = Encoding.ASCII.GetBytes(output);
            return Message.Create(id: id, type: type, rawData: raw);
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
