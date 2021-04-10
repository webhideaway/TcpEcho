using System;
using System.Threading.Tasks;

namespace TcpEcho
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var server = new Common.Server(1212);
            await server.ListenAsync(data => Console.OpenStandardOutput().Write(data, 0, data.Length), 3434);
        }
    }
}
