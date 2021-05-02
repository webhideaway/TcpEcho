using System.IO.Pipelines;
using System.Threading.Tasks;

namespace Common
{
    public interface IProcessor
    {
        Task ProcessMessagesAsync(PipeReader inputReader, PipeWriter outputWriter);
    }
}