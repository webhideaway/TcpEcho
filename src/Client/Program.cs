using System;
using System.Threading.Tasks;

namespace TcpEcho
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var client = new Common.Client(1212);
            await client.PostAsync(Console.OpenStandardInput(), 3434, 
                data => Console.OpenStandardOutput().Write(data, 0, data.Length));
        }
    }
}
