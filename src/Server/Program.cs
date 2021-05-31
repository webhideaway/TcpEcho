using DTO;
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Server
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var random = new Random();

            var localEndPoint = new IPEndPoint(IPAddress.Loopback, 1212);

            Console.WriteLine("Server listening for requests on local end point: {0}", localEndPoint);

            using var server = new ZeroPipeline.Server(localEndPoint);

            server.RegisterHandler<Person, Person>(request =>
            {
                var sleep = random.Next(1000, 15000);
                Thread.Sleep(sleep);
                if (request.Age < 18)
                {
                    var exception = $"Under aged person {request.Name}";
                    Console.WriteLine($"PERSON [RESPONSE ({sleep}ms)] [Exception = {exception}]");
                    throw new Exception(exception);
                }
                var response = new Person(request.Name.ToLowerInvariant(), request.Age * -1);
                Console.WriteLine($"PERSON [RESPONSE ({sleep}ms)] [Name = {response.Name}, Age = {response.Age}]");
                return response;

            });

            server.RegisterHandler<Car, Car>(request =>
            {
                var sleep = random.Next(1000, 15000);
                Thread.Sleep(sleep);
                if (request.Age > 5) 
                {
                    var exception = $"Over aged car {request.Reg}";
                    Console.WriteLine($"CAR [RESPONSE ({sleep}ms)] [Exception = {exception}]");
                    throw new Exception(exception); 
                }
                var response = new Car(request.Reg.ToLowerInvariant(), request.Age * -1);
                Console.WriteLine($"CAR [RESPONSE ({sleep}ms)] [Reg = {response.Reg}, Age = {response.Age}]");
                return response;
            });

            await server.ListenAsync(input: (type, request) =>
            {
                if (type.IsAssignableFrom(typeof(Person)))
                {
                    var person = (Person)request;
                    Console.WriteLine($"PERSON [REQUEST] [Name = {person.Name}, Age = {person.Age}]");
                }
                if (type.IsAssignableFrom(typeof(Car)))
                {
                    var car = (Car)request;
                    Console.WriteLine($"CAR [REQUEST] [Reg = {car.Reg}, Age = {car.Age}]");
                }
            });
        }
    }
}
