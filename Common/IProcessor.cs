using System;
using System.IO.Pipelines;
using System.Threading.Tasks;
using ZeroPipeline;

namespace Common
{
    public interface IProcessor
    {
        PipeWriter GetWriter(Message message);

        Task ProcessMessagesAsync(PipeReader reader, 
            Action<Message> input = null, Action<Message[]> outputs = null);
    }
}