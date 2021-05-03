using System;
using System.IO.Pipelines;
using System.Threading.Tasks;

namespace Common
{
    public interface IProcessor
    {
        Task ProcessMessagesAsync(PipeReader reader, Func<Message, PipeWriter> gettWriter);
    }
}