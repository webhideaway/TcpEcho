using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Threading.Tasks;

namespace ZeroPipeline.Interfaces
{
    public abstract class Processor : IProcessor
    {
        public async Task ProcessMessagesAsync(PipeReader reader,
            Action<Message> input = null, Action<Message, bool> output = null)
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

                        while (TryReadRequest(ref buffer, out Message request))
                        {
                            input?.Invoke(request);
                            var writer = GetWriter(request);
                            if (!writer.Equals(default))
                            {
                                var responses = await ProcessRequestAsync(request);
                                var count = responses.Length;
                                for (var index = 0; index < count; index++)
                                    output?.Invoke(responses[index], index == count - 1);

                                await foreach (var writeResult in WriteResponsesAsync(writer, responses))
                                    if (writeResult.IsCanceled || writeResult.IsCompleted)
                                        continue;
                            }
                        }

                        if (readResult.IsCompleted)
                            break;
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

        public abstract PipeWriter GetWriter(Message message);

        protected abstract bool TryReadRequest(ref ReadOnlySequence<byte> buffer, out Message request);

        protected abstract Task<Message[]> ProcessRequestAsync(Message request);

        protected abstract IAsyncEnumerable<FlushResult> WriteResponsesAsync(PipeWriter writer, params Message[] responses);
    }
}
