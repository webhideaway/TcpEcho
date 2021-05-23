using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using ZeroFormatter;
using ZeroFormatter.Internal;

namespace ZeroPipeline.Interfaces
{
    public abstract class Processor : IProcessor
    {
        private readonly ConcurrentDictionary<string, CancellationTokenSource> _cancellationTokenSources = new();

        protected abstract Task<Message[]> ProcessRequestAsync(Message request,
            CancellationToken cancellationToken = default);

        public async Task<SequencePosition> ProcessMessagesAsync(ReadOnlySequence<byte> buffer,
            Action<Message> input = null, Action<Message, bool> output = null)
        {
            while (TryReadRequest(ref buffer, out Message request))
            {
                RegisterCancellationTokenSource(request, out CancellationToken cancellationToken);

                input?.Invoke(request);
                var responses = await ProcessRequestAsync(request, cancellationToken);

                var writer = GetWriter(request);
                if (writer != null)
                {
                    var count = responses.Length;
                    for (var index = 0; index < count; index++)
                        output?.Invoke(responses[index], index == count - 1);

                    await foreach (var writeResult in WriteResponsesAsync(writer, responses))
                        if (writeResult.IsCanceled || writeResult.IsCompleted)
                            continue;
                }
            }

            return buffer.Start;
        }

        private void RegisterCancellationTokenSource(Message message, out CancellationToken cancellationToken)
        {
            cancellationToken = default;
            var type = Type.GetType(message.TypeName);
            if (type.IsAssignableFrom(typeof(CancellationToken)))
            {
                if (_cancellationTokenSources.TryRemove(message.Id, out CancellationTokenSource cancellationTokenSource))
                    using (cancellationTokenSource) cancellationTokenSource.Cancel(false);
            }
            else
            {
                var cancellationTokenSource = new CancellationTokenSource(message.Timeout);
                if (_cancellationTokenSources.TryAdd(message.Id, cancellationTokenSource))
                    cancellationToken = cancellationTokenSource.Token;
            }
        }

        private PipeWriter GetWriter(Message message)
        {
            IPEndPoint callbackEndPoint = null;

            if (message.CallbackAddress != null && message.CallbackPort > 0)
            {
                var callbackAddress = new IPAddress(message.CallbackAddress);
                callbackEndPoint = new IPEndPoint(callbackAddress, message.CallbackPort);
            }

            if (callbackEndPoint == null) return default;

            var callbackSocket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            callbackSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);

            callbackSocket.Connect(callbackEndPoint);
            var callbackStream = new NetworkStream(callbackSocket);

            var callbackWriter = new StreamPipeWriterOptions(leaveOpen: true);
            return PipeWriter.Create(callbackStream, callbackWriter);
        }

        private bool TryReadRequest(ref ReadOnlySequence<byte> buffer, out Message request)
        {
            request = default;
            var span = buffer.ToArray().AsSpan();

            var bomPos = span.IndexOf(Message.BOM);
            if (bomPos == -1) return false;

            var eomPos = span.IndexOf(Message.EOM);
            if (eomPos == -1) return false;

            var start = bomPos + Message.BOM.Length;
            var end = eomPos + Message.EOM.Length;
            var length = eomPos - start;

            var data = span.Slice(start, length).ToArray();
            request = ZeroFormatterSerializer.Deserialize<Message>(data);

            buffer = buffer.Slice(end);
            return true;
        }

        private async IAsyncEnumerable<FlushResult> WriteResponsesAsync(
            PipeWriter writer, Message[] responses)
        {
            foreach (var response in responses)
            {
                var data = new byte[] { };

                BinaryUtil.WriteBytes(ref data, 0, Message.BOM);
                ZeroFormatterSerializer.Serialize<Message>(ref data, Message.BOM.Length, response);
                BinaryUtil.WriteBytes(ref data, data.Length, Message.EOM);

                yield return await writer.WriteAsync(data);
            }
        }
    }
}
