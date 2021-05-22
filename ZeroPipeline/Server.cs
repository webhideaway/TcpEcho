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

        private object ProcessMessage(Message message)
        {
            CancellationTokenSources.TryRemove(message.Id, 
                out CancellationTokenSource cancellationTokenSource);

            var type = Type.GetType(message.TypeName);
            object value = null;

            if (type.IsAssignableFrom(typeof(Exception)))
            {
                value = Encoding.ASCII.GetString(message.RawData);
            }
            else if (type.IsAssignableFrom(typeof(CancellationToken)))
            {
                cancellationTokenSource?.Cancel();
            }
            else
            {
                value = _formatter.Deserialize(type, message.RawData);
            }

            cancellationTokenSource?.Dispose();
            return value;
        }

        public async Task ListenAsync(
            Action<object> input = null,
            Action<object, bool> output = null)
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

                        SequencePosition consumed = default;
                        try
                        {
                            if (readResult.IsCanceled)
                                break;

                            consumed = await ProcessMessagesAsync(buffer,
                                message => input?.Invoke(ProcessMessage(message)),
                                (message, done) => output?.Invoke(ProcessMessage(message), done)
                            );

                            if (readResult.IsCompleted)
                                break;
                        }
                        finally
                        {
                            reader.AdvanceTo(consumed, buffer.End);
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
            Action<Message> handler,
            CancellationToken cancellationToken = default)
        {
            var reader = await AcceptAsync();

            try
            {
                ReadResult readResult = await reader.ReadAsync(cancellationToken);
                ReadOnlySequence<byte> buffer = readResult.Buffer;

                try
                {
                    await ProcessMessagesAsync(buffer,
                        message => handler?.Invoke(message),
                        cancellationToken: cancellationToken
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
            CancellationToken cancellationToken = default)
        {
            var id = request.Id;
            var type = Type.GetType(request.TypeName);
            if (_registeredHandlers.TryGetValue(type, out Delegate handlers))
            {
                var invocationList = handlers.GetInvocationList();
                return await Task.WhenAll(invocationList.Select(handler =>
                    Task<Message>.Factory.StartNew(() =>
                    {
                        var type = Type.GetType(request.TypeName);
                        var input = _formatter.Deserialize(type, request.RawData);
                        var output = handler.DynamicInvoke(input);
                        var raw = _formatter.Serialize(type, output);
                        return Message.Create(id : id, type : type, rawData: raw);
                    }, 
                    cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Current).
                    ContinueWith<Message>(_ => {
                        var exception = _.Exception?.Flatten()?.GetBaseException() 
                            ?? new Exception(id, new TaskCanceledException(_));
                        var type = exception.GetType();
                        var output = $"{exception.Message}{Environment.NewLine}{exception.StackTrace}";
                        var raw = Encoding.ASCII.GetBytes(output);
                        return Message.Create(id: id, type: type, rawData: raw);
                    }, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted & TaskContinuationOptions.OnlyOnCanceled, TaskScheduler.Current)
                ));
            }
            return new Message[] { };
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
