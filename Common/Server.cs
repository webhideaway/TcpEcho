using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.IO.Pipelines;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Common
{
    public class Server
    {
        private readonly Socket _listenSocket;
        private readonly IFormatter _formatter;

        private readonly ConcurrentDictionary<EndPoint, Client> _callbackClients
            = new ConcurrentDictionary<EndPoint, Client>();
        private readonly ConcurrentDictionary<Type, Delegate> _registeredHandlers
            = new ConcurrentDictionary<Type, Delegate>();

        public Server(EndPoint listenEndPoint, IFormatter formatter = null)
        {
            _listenSocket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            _listenSocket.Bind(listenEndPoint);

            Console.WriteLine($"Listening on local end point {listenEndPoint}");

            _listenSocket.Listen(120);
            _formatter = formatter ?? new DefaultFormatter();
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

                while (TryReadRequest(ref buffer, out ReadOnlyMemory<byte> request))
                {
                    // Process the request.
                    await ProcessRequestAsync(request);
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

        private bool TryReadRequest(ref ReadOnlySequence<byte> buffer, out ReadOnlyMemory<byte> request)
        {
            // Look for a EOL in the buffer.
            SequencePosition? charPos = buffer.PositionOf((byte)'\n');

            if (charPos == null)
            {
                request = default;
                return false;
            }

            // Skip the request + the \n.
            SequencePosition seqPos = buffer.GetPosition(1, charPos.Value);
            request = buffer.Slice(0, seqPos).ToArray();
            buffer = buffer.Slice(seqPos);
            return true;
        }

        private async Task ProcessRequestAsync(ReadOnlyMemory<byte> request)
        {
            var callbackEndPoint = new IPEndPoint(IPAddress.Loopback, 3434);
            var response = request;

            if (_registeredHandlers.TryGetValue(request.GetType(), out Delegate handler))
                handler?.DynamicInvoke(response);

            if (callbackEndPoint == null) return;

            var callbackClient = _callbackClients.GetOrAdd(
                callbackEndPoint, new Client(callbackEndPoint));

            await callbackClient.PostAsync(response.ToArray(), null);
        }
    }
}
