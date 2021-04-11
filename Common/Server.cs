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
        private Lazy<Client> _callbackClient;

        public Server(int localPort)
        {
            _listenSocket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            _listenSocket.Bind(new IPEndPoint(IPAddress.Loopback, localPort));

            Console.WriteLine($"Listening on local port {localPort}");

            _listenSocket.Listen(120);
        }

        public async Task ListenAsync(Action<ReadOnlyMemory<byte>> handler = null, int? callbackPort = null)
        {
            _callbackClient = new Lazy<Client>(() =>
                callbackPort.HasValue ? new Client(callbackPort.Value) : null);

            while (true)
            {
                var socket = await _listenSocket.AcceptAsync();
                await ProcessLinesAsync(socket, handler);
            }
        }

        private async Task ProcessLinesAsync(Socket socket, Action<ReadOnlyMemory<byte>> handler = null)
        {
            // Create a PipeReader over the network stream
            var stream = new NetworkStream(socket);
            var reader = PipeReader.Create(stream);

            while (true)
            {
                ReadResult result = await reader.ReadAsync();
                ReadOnlySequence<byte> buffer = result.Buffer;

                while (TryReadLine(ref buffer, out ReadOnlySequence<byte> line))
                {
                    // Process the line.
                    if (TryProcessLine(line, out ReadOnlyMemory<byte> data))
                    {
                        handler?.Invoke(data);
                        if (_callbackClient.Value != null)
                            await _callbackClient.Value.PostAsync(data.ToArray());
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

        private static bool TryReadLine(ref ReadOnlySequence<byte> buffer, out ReadOnlySequence<byte> line)
        {
            // Look for a EOL in the buffer.
            SequencePosition? charPos = buffer.PositionOf((byte)'\n');

            if (charPos == null)
            {
                line = default;
                return false;
            }

            // Skip the line + the \n.
            SequencePosition seqPos = buffer.GetPosition(1, charPos.Value);
            line = buffer.Slice(0, seqPos);
            buffer = buffer.Slice(seqPos);
            return true;
        }

        private static bool TryProcessLine(in ReadOnlySequence<byte> line, out ReadOnlyMemory<byte> data)
        {
            if (line.Equals(default))
            {
                data = default;
                return false;
            }
            
            data = line.ToArray();
            return true;
        }
    }
}
