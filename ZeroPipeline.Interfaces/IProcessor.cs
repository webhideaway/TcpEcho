using System;
using System.IO.Pipelines;
using System.Threading.Tasks;

namespace ZeroPipeline.Interfaces
{
    public interface IProcessor
    {
        PipeWriter GetWriter(Message message);

        Task ProcessMessagesAsync(PipeReader reader,
            Action<Message> input = null, Action<Message[]> outputs = null);
    }
}