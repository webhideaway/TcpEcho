using System.Threading.Tasks;

namespace TcpEcho
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var client = new Common.Client(1212);
            await client.PostAsync(3434);
        }
    }
}
