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
            
            await server.ListenAsync(data => 
                Console.Write(Encoding.UTF8.GetString(data.ToArray())), 
                    new IPEndPoint(IPAddress.Loopback, 3434));
        }
    }
}
