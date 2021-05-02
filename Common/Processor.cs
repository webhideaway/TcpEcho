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
    public class Processor : IProcessor
    {
        private readonly Func<ReadOnlySequence<byte>, Message> _inputHandler;
        private readonly Func<Message, ReadOnlyMemory<byte>> _outputHandler;

        public Processor(
            Func<ReadOnlySequence<byte>, Message> inputHandler,
            Func<Message, ReadOnlyMemory<byte>> outputHandler)
        {
            _inputHandler = inputHandler;
            _outputHandler = outputHandler;
        }

        public async Task ProcessMessagesAsync(
            PipeReader inputReader,
            PipeWriter outputWriter)
        {
            try
            {
                while (true)
                {
                    ReadResult readResult = await inputReader.ReadAsync();
                    ReadOnlySequence<byte> buffer = readResult.Buffer;

                    try
                    {
                        if (readResult.IsCanceled)
                        {
                            break;
                        }

                        if (TryParseMessage(_inputHandler, ref buffer, out Message message))
                        {
                            FlushResult flushResult = await WriteMessagesAsync(_outputHandler, outputWriter, message);

                            if (flushResult.IsCanceled || flushResult.IsCompleted)
                            {
                                break;
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
                        inputReader.AdvanceTo(buffer.Start, buffer.End);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
            }
            finally
            {
                await inputReader.CompleteAsync();
                await outputWriter.CompleteAsync();
            }
        }

        static bool TryParseMessage(
            Func<ReadOnlySequence<byte>, Message> inputHandler,
            ref ReadOnlySequence<byte> buffer,
            out Message message) =>
            !(message = inputHandler(buffer)).Equals(default(Message));

        static ValueTask<FlushResult> WriteMessagesAsync(
            Func<Message, ReadOnlyMemory<byte>> outputHandler,
            PipeWriter writer,
            Message message) =>
            writer.WriteAsync(outputHandler(message));
    }
}
