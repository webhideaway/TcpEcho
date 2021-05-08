using System;
using System.IO;
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

            await server.ListenAsync(response => {
                if (response.GetType().IsAssignableFrom(typeof(DTO.Person)))
                {
                    var person = (DTO.Person)response;
                    Console.WriteLine($"PERSON #{Interlocked.Increment(ref person_count)} [Name = {person.Name}, Age = {person.Age}]");
                }
                if (response.GetType().IsAssignableFrom(typeof(DTO.Car)))
                {
                    var car = (DTO.Car)response;
                    Console.WriteLine($"CAR #{Interlocked.Increment(ref car_count)} [Brand = {car.Brand}, Age = {car.Age}]");
                }
            });
        }
    }
}
