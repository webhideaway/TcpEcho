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
    public class Client : IDisposable
    {
        private readonly Socket _remoteSocket;
        private readonly PipeWriter _remoteWriter;
        private Server _callbackListener;
        private bool _disposedValue;
        private readonly IPEndPoint _callbackEndPoint;
        private readonly IFormatter _formatter;

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
                _callbackListener = new Server(_callbackEndPoint, formatter: _formatter);
        }

        private ReadOnlyMemory<byte> ProcessRequest<TRequest>(TRequest request)
        {
            var raw = _formatter.Serialize<TRequest>(request);
            var message = Message.Create<TRequest>(raw, _callbackEndPoint);
            var data = ZeroFormatterSerializer.Serialize<Message>(message);
            BinaryUtil.WriteByte(ref data, data.Length, Message.EOM);
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
            await Task.WhenAll(
                _remoteWriter.WriteAsync(data).AsTask(),
                _callbackListener.ListenAsync(response => {
                    if (response.GetType().IsAssignableFrom(typeof(Exception)))
                        throw (Exception)response;
                    else if (response.GetType().IsAssignableFrom(typeof(TResponse)))
                        handler((TResponse)response);
                    else
                        throw new InvalidDataException($"Unexpected data type received {response.GetType()}");
                })
            );
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
