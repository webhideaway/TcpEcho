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
            var client = new Common.Client(
                new IPEndPoint(IPAddress.Loopback, 1212),
                new IPEndPoint(IPAddress.Loopback, 3434));

            while(true)
                await client.PostAsync<string, string>(Console.ReadLine());
        }
    }
}
