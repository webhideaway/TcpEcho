using System;
using System.Buffers;
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
        private Lazy<Client> _callbackPoster;
        private readonly IFormatter _formatter;

        public Server(EndPoint listenEndPoint, IFormatter formatter = null)
        {
            _listenSocket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            _listenSocket.Bind(listenEndPoint);

            Console.WriteLine($"Listening on local end point {listenEndPoint}");

            _listenSocket.Listen(120);
            _formatter = formatter ?? new DefaultFormatter();
        }

        public async Task ListenAsync<TRequest, TResponse>(Action<TResponse> handler)
        {
            await Task.CompletedTask;
        }

        public async Task ListenAsync(Action<ReadOnlyMemory<byte>> handler = null, EndPoint callbackEndPoint = null)
        {
            _callbackPoster = new Lazy<Client>(() =>
                callbackEndPoint == null ? null : new Client(callbackEndPoint));

            while (true)
            {
                var socket = await _listenSocket.AcceptAsync();
                await ProcessRequestsAsync(socket, handler);
            }
        }

        private async Task ProcessRequestsAsync(Socket socket, Action<ReadOnlyMemory<byte>> handler = null)
        {
            // Create a PipeReader over the network stream
            var stream = new NetworkStream(socket);
            var reader = PipeReader.Create(stream);

            while (true)
            {
                ReadResult result = await reader.ReadAsync();
                ReadOnlySequence<byte> buffer = result.Buffer;

                while (TryReadRequest(ref buffer, out ReadOnlySequence<byte> request))
                {
                    // Process the request.
                    if (TryProcessRequest(request, out ReadOnlyMemory<byte> response))
                    {
                        handler?.Invoke(response);
                        if (_callbackPoster.Value != null)
                            await _callbackPoster.Value.PostAsync(response.ToArray());
                    }
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

        private static bool TryReadRequest(ref ReadOnlySequence<byte> buffer, out ReadOnlySequence<byte> request)
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
            request = buffer.Slice(0, seqPos);
            buffer = buffer.Slice(seqPos);
            return true;
        }

        private static bool TryProcessRequest(in ReadOnlySequence<byte> request, out ReadOnlyMemory<byte> response)
        {
            if (request.Equals(default))
            {
                response = default;
                return false;
            }
            
            response = request.ToArray();
            return true;
        }
    }
}
