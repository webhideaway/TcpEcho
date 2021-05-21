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
        private Socket _remoteSocket;
        private PipeWriter _remoteWriter;
        private Server _callbackListener;
        private bool _disposedValue;
        private IPEndPoint _callbackEndPoint;
        private IFormatter _formatter;
        private static ConcurrentDictionary<string, BlockingCollection<object>> _callbackResponses = new();

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

            var data = new byte[] { };

            BinaryUtil.WriteBytes(ref data, 0, Message.BOM);
            ZeroFormatterSerializer.Serialize(ref data, Message.BOM.Length, message);
            BinaryUtil.WriteBytes(ref data, data.Length, Message.EOM);

            return data;
        }

        private async Task PostAsync<TRequest>(TRequest request, Action<string, BlockingCollection<object>> handler)
        {
            var data = ProcessRequest(request, out string id);
            var callbackResponses = new BlockingCollection<object>();
            if (_callbackResponses.TryAdd(id, callbackResponses))
            {
                if (_callbackEndPoint == null) 
                    callbackResponses.CompleteAdding();
            }
            await _remoteWriter.WriteAsync(data);
            handler?.Invoke(id, callbackResponses);
        }

        public async Task PostAsync<TRequest>(TRequest request, Action<Type, object> responseHandler = null)
        {
            await Task.WhenAll(
                PostAsync(request, (id, callbackResponses) =>
                {
                    using (callbackResponses)
                    {
                        var responses = callbackResponses.GetConsumingEnumerable();
                        foreach (var response in responses)
                            responseHandler?.Invoke(response.GetType(), response);
                    }
                }),
                _callbackListener.ListenAsync(input: (id, callbackResponse, count) =>
                {
                    if (_callbackResponses.TryGetValue(id,
                        out BlockingCollection<object> callbackResponses))
                    {
                        callbackResponses.TryAdd(callbackResponse);
                        if (callbackResponses.Count == count)
                            callbackResponses.CompleteAdding();
                    }
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
