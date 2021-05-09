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
        public async Task ProcessMessagesAsync(PipeReader reader, 
            Action<Message> input = null, Action<Message[]> outputs = null)
        {
            try
            {
                while (true)
                {
                    ReadResult readResult = await reader.ReadAsync();
                    ReadOnlySequence<byte> buffer = readResult.Buffer;
                    int position = 0;

                    try
                    {
                        if (readResult.IsCanceled)
                            break;

                        while (TryReadMessage(buffer, ref position, out Message message))
                        {
                            input?.Invoke(message);
                            var messages = await ProcessMessageAsync(message);
                            outputs?.Invoke(messages);
                            
                            await foreach (var writeResult in WriteMessagesAsync(message, messages))
                                if (writeResult.IsCanceled || writeResult.IsCompleted)
                                    continue;

                            reader.AdvanceTo(new SequencePosition(buffer, position));
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
                        reader.AdvanceTo(buffer.End);
                    }
                }
            }
            finally
            {
                await reader.CompleteAsync();
            }
        }

        public abstract PipeWriter GetWriter(Message message);

        protected abstract bool TryReadMessage(ReadOnlySequence<byte> buffer, ref int position, out Message message);

        protected abstract Task<Message[]> ProcessMessageAsync(Message message);

        protected abstract IAsyncEnumerable<FlushResult> WriteMessagesAsync(Message input, params Message[] outputs);
    }
}
