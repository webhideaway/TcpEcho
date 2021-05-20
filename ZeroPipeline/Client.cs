using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using ZeroFormatter;
using ZeroFormatter.Internal;
using ZeroPipeline.Interfaces;

namespace ZeroPipeline
{
    public class Client : IDisposable
    {
        private readonly Socket _remoteSocket;
        private readonly PipeWriter _remoteWriter;
        private Server _callbackListener;
        private bool _disposedValue;
        private readonly IPEndPoint _callbackEndPoint;
        private readonly IFormatter _formatter;
        private readonly Task _listenerTask;
        private ConcurrentDictionary<string, Task<object[]>> _callbackTasks = new();

        public Client(IPEndPoint remoteEndPoint, IPEndPoint callbackEndPoint = null, IFormatter formatter = null)
        {
            _remoteSocket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            _remoteSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);

            _remoteSocket.Connect(remoteEndPoint);
            var remoteStream = new NetworkStream(_remoteSocket);

            _remoteWriter = PipeWriter.Create(remoteStream, new StreamPipeWriterOptions(leaveOpen: true));

            _callbackEndPoint = callbackEndPoint;
            _formatter = formatter ?? new DefaultFormatter();

            if (_callbackEndPoint != null)
            {
                _callbackListener = new Server(_callbackEndPoint, formatter: _formatter);
                _listenerTask = _callbackListener.ListenAsync(output: (callbackId, responses) => {
                    _callbackTasks.TryAdd(callbackId, Task<object[]>.Factory.StartNew(() => responses));
                });
            }
        }

        private ReadOnlyMemory<byte> ProcessRequest<TRequest>(TRequest request, out string id)
        {
            var raw = _formatter.Serialize(request);
            var message = Message.Create<TRequest>(raw, _callbackEndPoint);
            id = message.Id;

            var data = new byte[] { };

            BinaryUtil.WriteBytes(ref data, 0, Message.BOM);
            ZeroFormatterSerializer.Serialize(ref data, Message.BOM.Length, message);
            BinaryUtil.WriteBytes(ref data, data.Length, Message.EOM);

            return data;
        }

        public async Task<string> PostAsync<TRequest>(TRequest request)
        {
            var data = ProcessRequest(request, out string id);
            await _remoteWriter.WriteAsync(data);
            return id;
        }

        public async Task PostAsync<TRequest, TResponse>(TRequest request, 
            Action<TResponse> responseHandler, Action<string> exceptionHandler = null)
        {
            var id = await PostAsync(request);
            if (_callbackTasks.TryGetValue(id, out Task<object[]> callbackTask))
            {
                var responses = await callbackTask;
                foreach (var response in responses)
                {
                    if (response.GetType().IsAssignableFrom(typeof(Exception)))
                        exceptionHandler?.Invoke((string)response);
                    else if (response.GetType().IsAssignableFrom(typeof(TResponse)))
                        responseHandler?.Invoke((TResponse)response);
                    else
                        throw new InvalidDataException($"Unexpected data type received {response.GetType()}");
                }
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                    _remoteSocket?.Dispose();
                    _formatter?.Dispose();
                    _callbackListener?.Dispose();
                    _listenerTask?.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                _disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~Client()
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
