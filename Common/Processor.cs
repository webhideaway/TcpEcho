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
        public async Task ProcessMessagesAsync(PipeReader reader, Func<Message, PipeWriter> gettWriter)
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
                        {
                            break;
                        }

                        while (TryReadMessage(ref buffer, out Message message))
                        {
                            var writer = gettWriter(message);

                            try
                            {
                                FlushResult[] flushResults = await ProcessMessageAsync(writer, message);

                                if (flushResults.All(flushResult => flushResult.IsCanceled || flushResult.IsCompleted))
                                {
                                    break;
                                }
                            }
                            finally 
                            {
                                await writer.CompleteAsync();
                            }
                        }

                        if (readResult.IsCompleted)
                        {
                            if (!buffer.IsEmpty)
                            {
                                throw new InvalidDataException("Incomplete message.");
                            }
                            break;
                        }
                    }
                    finally
                    {
                        reader.AdvanceTo(buffer.Start, buffer.End);
                    }
                }
            }
            finally
            {
                await reader.CompleteAsync();
            }
        }

        protected abstract bool TryReadMessage(
            ref ReadOnlySequence<byte> buffer,
            out Message message);

        protected abstract Task<FlushResult[]> ProcessMessageAsync(
            PipeWriter writer,
            Message message);
    }
}
