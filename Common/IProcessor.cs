using System;
using System.IO.Pipelines;
using System.Threading.Tasks;

namespace Common
{
    public interface IProcessor
    {
        PipeWriter GetWriter(Message message);

        Task ProcessMessagesAsync(PipeReader reader, Action<Message> handler = null);
    }
}