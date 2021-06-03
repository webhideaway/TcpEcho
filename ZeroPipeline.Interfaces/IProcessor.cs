using System;
using System.Threading;
using System.Threading.Tasks;

namespace ZeroPipeline.Interfaces
{
    public interface IProcessor
    {
        Task ProcessMessageAsync(
            Message message,
            Action<Message> input = null,
            Action<Message, bool> output = null,
            CancellationToken cancellationToken = default);
    }
}