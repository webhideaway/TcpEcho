using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common
{
    public abstract class Processor : IProcessor
    {
        public async Task ProcessMessagesAsync(PipeReader reader, Action<Message> handler = null)
        {
            try
            {
                while (true)
                {
                    ReadResult readResult = await reader.ReadAsync();
                    ReadOnlySequence<byte> buffer = readResult.Buffer;

                    try
                    {
                        if (readResult.IsCanceled)
                            break;

                        while (TryReadMessage(ref buffer, out Message message))
                        {
                            if (handler == null)
                            {
                                var writer = GetWriter(message);

                                try
                                {
                                    var messages = await ProcessMessageAsync(message);
                                    await foreach (var writeResult in WriteMessagesAsync(writer, messages))
                                        if (writeResult.IsCanceled || writeResult.IsCompleted)
                                            continue;
                                }
                                finally
                                {
                                    await writer.FlushAsync();
                                }
                            }
                            else
                            {
                                handler(message);
                            }
                        }

                        if (readResult.IsCompleted)
                        {
                            if (!buffer.IsEmpty)
                                throw new InvalidDataException("Incomplete message.");
                            break;
                        }
                    }
                    finally
                    {
                        if (buffer.Length > 0)
                            reader.AdvanceTo(buffer.Start, buffer.End);
                    }
                }
            }
            finally
            {
                await reader.CompleteAsync();
            }
        }

        public abstract PipeWriter GetWriter(Message message);

        protected abstract bool TryReadMessage(ref ReadOnlySequence<byte> buffer, out Message message);

        protected abstract Task<Message[]> ProcessMessageAsync(Message message);

        protected abstract IAsyncEnumerable<FlushResult> WriteMessagesAsync(PipeWriter writer, params Message[] messages);
    }
}
