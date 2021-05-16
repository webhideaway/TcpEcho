using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace TcpEcho
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var remoteEndPoint = new IPEndPoint(IPAddress.Loopback, 1212);
            //var callbackEndPoint = new IPEndPoint(IPAddress.Loopback, 3434);

            Console.WriteLine("Client posting requests to remote end point: {0}", remoteEndPoint);
            //Console.WriteLine("Client listening for callbacks on callback end point: {0}", callbackEndPoint);

            using var client = new Common.Client(remoteEndPoint); //, callbackEndPoint);

            var random = new Random();
            var stopwatch = new Stopwatch();

            var range = random.Next(5, 15);

            int person_count = 0;
            int car_count = 0;

            while (true)
            {
                Console.Read();

                stopwatch.Start();
                foreach (var idx in Enumerable.Range(0, range).
                    AsParallel().WithDegreeOfParallelism(Environment.ProcessorCount).
                    WithExecutionMode(ParallelExecutionMode.ForceParallelism))
                {
                    if (idx % 2 == 0)
                    {
                        var person = new DTO.Person
                        (
                            name: new string(Enumerable.Range(1, random.Next(5, 15)).
                                Select(_ => Convert.ToChar(random.Next(65, 90))).ToArray()),
                            age: random.Next(1, 100)
                        );

                        Console.WriteLine($"PERSON #{Interlocked.Increment(ref person_count)} [Name = {person.Name}, Age = {person.Age}]");

                        await client.PostAsync<DTO.Person>(person);
                    }
                    else
                    {
                        var car = new DTO.Car
                        (
                            brand: new string(Enumerable.Range(1, random.Next(5, 15)).
                                Select(_ => Convert.ToChar(random.Next(65, 90))).ToArray()),
                            age: random.Next(1, 10)
                        );

                        Console.WriteLine($"CAR #{Interlocked.Increment(ref car_count)} [Brand = {car.Brand}, Age = {car.Age}]");

                        await client.PostAsync<DTO.Car>(car);
                    }
                }
                stopwatch.Stop();

                Console.WriteLine($"Execution: {range / stopwatch.Elapsed.TotalMilliseconds} items/ms");

                //Thread.Sleep(random.Next(500, 1500));
            }
        }
    }
}
