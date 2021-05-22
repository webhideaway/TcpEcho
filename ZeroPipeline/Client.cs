using System;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ZeroFormatter;
using ZeroFormatter.Internal;
using ZeroPipeline.Interfaces;

namespace ZeroPipeline
{
    public class Client : IDisposable
    {
        private Socket _remoteSocket;
        private PipeWriter _remoteWriter;
        private Server _callbackListener;
        private bool _disposedValue;
        private IPEndPoint _callbackEndPoint;
        private IFormatter _formatter;

        public Client(IPEndPoint remoteEndPoint, IPEndPoint callbackEndPoint = null, IFormatter formatter = null)
        {
            SetRemoteWriter(remoteEndPoint);

            _formatter = formatter ?? new DefaultFormatter();
            _callbackEndPoint = callbackEndPoint;

            if (_callbackEndPoint != null)
                _callbackListener = new Server(_callbackEndPoint, formatter: _formatter);
        }

        private void SetRemoteWriter(IPEndPoint remoteEndPoint)
        {
            _remoteSocket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            _remoteSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);

            _remoteSocket.Connect(remoteEndPoint);
            var remoteStream = new NetworkStream(_remoteSocket);

            _remoteWriter = PipeWriter.Create(remoteStream, new StreamPipeWriterOptions(leaveOpen: true));
        }

        private ReadOnlyMemory<byte> ProcessRequest<TRequest>(TRequest request, out string id)
        {
            var raw = _formatter.Serialize(request);
            var message = Message.Create<TRequest>(raw, _callbackEndPoint);
            id = message.Id;

            var data = new byte[512];

            BinaryUtil.WriteBytes(ref data, 0, Message.BOM);
            ZeroFormatterSerializer.Serialize(ref data, Message.BOM.Length, message);
            BinaryUtil.WriteBytes(ref data, data.Length, Message.EOM);

            return data;
        }

        private object ProcessMessage(Message message)
        {
            var type = Type.GetType(message.TypeName);
            return type.IsAssignableFrom(typeof(Exception))
                ? Encoding.ASCII.GetString(message.RawData)
                : _formatter.Deserialize(type, message.RawData);
        }

        public async Task PostAsync<TRequest>(TRequest request, 
            Action<Type, object> responseHandler = null, 
            CancellationToken cancellationToken = default)
        {
            var data = ProcessRequest(request, out string id);
            if (_callbackEndPoint == null)
                await _remoteWriter.WriteAsync(data, cancellationToken);
            else
            {
                await Task.WhenAll(
                    _remoteWriter.WriteAsync(data, cancellationToken).AsTask(),
                    _callbackListener.CallbackAsync(callback =>
                    {
                        if (id == callback.Id)
                        {
                            var response = ProcessMessage(callback);
                            responseHandler?.Invoke(response.GetType(), response);
                        }
                    }, cancellationToken)
                );
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
