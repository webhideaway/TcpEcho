using System.Net;
using System.Threading.Tasks;

namespace TcpEcho
{
    class Program
    {
        static async Task Main(string[] args)
        {
            using var server = new Common.Server(
                new IPEndPoint(IPAddress.Loopback, 1212));

            server.RegisterHandler<string, string>(data => data.ToUpperInvariant());
            await server.ListenAsync();
        }
    }
}
