using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace TcpEcho
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var localEndPoint = new IPEndPoint(IPAddress.Loopback, 1212);

            Console.WriteLine("Server listening for requests on local end point: {0}", localEndPoint);

            using var server = new Common.Server(localEndPoint);

            int person_count = 0;
            int car_count = 0;

            server.RegisterHandler<DTO.Person>(person =>
                Console.WriteLine($"PERSON #{Interlocked.Increment(ref person_count)} [Name = {person.Name}, Age = {person.Age}]")
            );

            server.RegisterHandler<DTO.Car>(car =>
                Console.WriteLine($"CAR #{Interlocked.Increment(ref car_count)} [Brand = {car.Brand}, Age = {car.Age}]")
            );

            await server.ListenAsync();
        }
    }
}
