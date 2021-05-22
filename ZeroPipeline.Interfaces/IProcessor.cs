using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Threading.Tasks;

namespace ZeroPipeline.Interfaces
{
    public interface IProcessor
    {
        PipeWriter GetWriter(Message message);

        Task ProcessMessagesAsync(ReadOnlySequence<byte> buffer,
            Action<Message> input = null, Action<Message, bool> output = null);
    }
}