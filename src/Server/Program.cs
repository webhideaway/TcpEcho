using System;
using System.Text;
using System.Threading.Tasks;

namespace TcpEcho
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var server = new Common.Server(1212);
            await server.ListenAsync(data => 
                Console.WriteLine(Encoding.UTF8.GetString(data.ToArray())), 3434);
        }
    }
}
