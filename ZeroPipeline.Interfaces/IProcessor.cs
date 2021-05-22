using System;
using System.Buffers;
using System.Threading;
using System.Threading.Tasks;

namespace ZeroPipeline.Interfaces
{
    public interface IProcessor
    {
        Task<SequencePosition> ProcessMessagesAsync(
            ReadOnlySequence<byte> buffer,
            Action<Message> input = null,
            Action<Message, bool> output = null);
    }
}