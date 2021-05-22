using System;
using System.Buffers;
using System.Threading.Tasks;

namespace ZeroPipeline.Interfaces
{
    public interface IProcessor
    {
        Task ProcessMessagesAsync(ReadOnlySequence<byte> buffer,
            Action<Message> input = null, Action<Message, bool> output = null);
    }
}