using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace TcpEcho
{
    class Program
    {
        static async Task Main(string[] args)
        {
            await Common.Client.RunAsync(1212); //.ContinueWith(async t => await Common.Server.RunAsync(3434));
        }
    }
}
