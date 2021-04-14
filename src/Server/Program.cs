using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace TcpEcho
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var server = new Common.Server(
                new IPEndPoint(IPAddress.Loopback, 1212));

            server.RegisterHandler<ReadOnlyMemory<byte>>(data =>
                Console.Write(Encoding.UTF8.GetString(data.ToArray())));
            
            await server.ListenAsync();
        }
    }
}
