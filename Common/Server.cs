using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.IO.Pipelines;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Common
{
    public class Server
    {
        private readonly Socket _listenSocket;
        private readonly IFormatter _formatter;

        private readonly ConcurrentDictionary<IPEndPoint, Client> _callbackClients
            = new ConcurrentDictionary<IPEndPoint, Client>();
        private readonly ConcurrentDictionary<Type, Delegate> _registeredHandlers
            = new ConcurrentDictionary<Type, Delegate>();

        public Server(IPEndPoint listenEndPoint, IFormatter formatter = null)
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
                await ProcessRequestsAsync(socket);
            }
        }

        private async Task ProcessRequestsAsync(Socket socket)
        {
            // Create a PipeReader over the network stream
            var stream = new NetworkStream(socket);
            var reader = PipeReader.Create(stream);

            while (true)
            {
                ReadResult result = await reader.ReadAsync();
                ReadOnlySequence<byte> buffer = result.Buffer;

                while (TryReadMessage(ref buffer, out Message message))
                {
                    // Process the message.
                    await ProcessMessageAsync(message);
                }

                // Tell the PipeReader how much of the buffer has been consumed.
                reader.AdvanceTo(buffer.Start, buffer.End);

                // Stop reading if there's no more data coming.
                if (result.IsCompleted)
                {
                    break;
                }
            }

            // Mark the PipeReader as complete.
            await reader.CompleteAsync();
        }

        private bool TryReadMessage(ref ReadOnlySequence<byte> buffer, out Message message)
        {
            var size = Marshal.SizeOf(typeof(Message));
            if (buffer.Length < size)
            {
                message = default;
                return false;
            }

            var input = buffer.Slice(0, size).ToArray();
            message = _formatter.Deserialize<Message>(input);

            buffer = buffer.Slice(size);
            return true;
        }

        private async Task ProcessMessageAsync(Message message)
        {
            var request = _formatter.Deserialize(Type.GetType(message.Type), message.Raw);
            var responses = new object[] { };

            if (_registeredHandlers.TryGetValue(request.GetType(), out Delegate handlers))
                responses = await Task.WhenAll(handlers.GetInvocationList().Select(handler =>
                    Task.Factory.StartNew(() => handler.DynamicInvoke(request))));

            IPEndPoint callbackEndPoint = null;

            if (message.Address != null && message.Port > 0)
            {
                var callbackAddress = new IPAddress(message.Address);
                callbackEndPoint = new IPEndPoint(callbackAddress, message.Port);
            }

            if (callbackEndPoint == null) return;

            var callbackClient = _callbackClients.GetOrAdd(
                callbackEndPoint, new Client(callbackEndPoint));

            foreach (var response in responses)
            {
                if (response == null) continue;
                var output = _formatter.Serialize(response);
                await callbackClient.PostAsync(output);
            }
        }
    }
}
