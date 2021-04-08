using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
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

        public async Task ListenAsync(int? callbackPort = null)
        {
            _callbackClient = new Lazy<Client>(() =>
                callbackPort.HasValue ? new Client(callbackPort.Value) : null);

            while (true)
            {
                var socket = await _listenSocket.AcceptAsync();
                _ = ProcessLinesAsync(socket);
            }
        }

        private async Task ProcessLinesAsync(Socket socket)
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
                    _ = ProcessLineAsync(line).ContinueWith(async t => await (
                        _callbackClient.Value == null ? Task.CompletedTask : _callbackClient.Value.PostAsync()
                    ));
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
            SequencePosition? position = buffer.PositionOf((byte)'\n');

            if (position == null)
            {
                line = default;
                return false;
            }

            // Skip the line + the \n.
            line = buffer.Slice(0, position.Value);
            buffer = buffer.Slice(buffer.GetPosition(1, position.Value));
            return true;
        }

        private async Task<byte[]> ProcessLineAsync(ReadOnlySequence<byte> buffer)
        {
            return await Task.FromResult(buffer.ToArray());
        }
    }
}
