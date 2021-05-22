using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.IO.Pipelines;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
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

        private object ProcessMessage(Message message)
        {
            var type = Type.GetType(message.TypeName);
            return type.IsAssignableFrom(typeof(Exception))
                ? Encoding.ASCII.GetString(message.RawData)
                : _formatter.Deserialize(type, message.RawData);
        }

        public async Task ListenAsync(
            Action<string, object, int> input = null, 
            Action<string, object, bool> output = null)
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
                                message => input?.Invoke(message.Id, ProcessMessage(message), message.Count),
                                (message, done) => output?.Invoke(message.Id, ProcessMessage(message), done)
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
            Action<string, object, int> handler)
        {
            var reader = await AcceptAsync();

            try
            {
                ReadResult readResult = await reader.ReadAsync();
                ReadOnlySequence<byte> buffer = readResult.Buffer;

                try
                {
                    await ProcessMessagesAsync(buffer,
                        message => handler?.Invoke(message.Id, ProcessMessage(message), message.Count)
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

        protected override async Task<Message[]> ProcessRequestAsync(Message request)
        {
            var id = request.Id;
            var type = Type.GetType(request.TypeName);
            if (_registeredHandlers.TryGetValue(type, out Delegate handlers))
            {
                var invocationList = handlers.GetInvocationList();
                var count = invocationList.Length;
                return await Task.WhenAll(invocationList.Select(handler =>
                    Task.Factory.StartNew(() =>
                    {
                        try
                        {
                            var type = Type.GetType(request.TypeName);
                            var input = _formatter.Deserialize(type, request.RawData);
                            var output = handler.DynamicInvoke(input);
                            var raw = _formatter.Serialize(type, output);
                            return Message.Create(id, count, type, raw);
                        }
                        catch (Exception ex)
                        {
                            var exception = ex.GetBaseException();
                            var type = exception.GetType();
                            var output = $"{exception.Message}{Environment.NewLine}{exception.StackTrace}";
                            var raw = Encoding.ASCII.GetBytes(output);
                            return Message.Create(id, count, type, raw);
                        }
                    })
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
