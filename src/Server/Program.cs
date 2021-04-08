using System.Threading.Tasks;

namespace TcpEcho
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var server = new Common.Server(1212);
            await server.ListenAsync(3434);
        }
    }
}
