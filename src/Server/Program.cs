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
                Thread.Sleep(random.Next(1000, 5000));
                if (request.Age < 18) throw new Exception($"Under aged person {request.Name}");
                return new Person(request.Name.ToLowerInvariant(), request.Age * -1);

            });

            server.RegisterHandler<Car, Car>(request =>
            {
                Thread.Sleep(random.Next(1000, 5000));
                if (request.Age < 3) throw new Exception($"Under aged car {request.Brand}");
                return new Car(request.Brand.ToLowerInvariant(), request.Age * -1);
            });

            await server.ListenAsync(input: (type, request) =>
            {
                if (type.IsAssignableFrom(typeof(Person)))
                {
                    var person = (Person)request;
                    Console.WriteLine($"PERSON [RECEIVED] [Name = {person.Name}, Age = {person.Age}]");
                }
                if (type.IsAssignableFrom(typeof(Car)))
                {
                    var car = (Car)request;
                    Console.WriteLine($"CAR [RECEIVED] [Brand = {car.Brand}, Age = {car.Age}]");
                }
            });
        }
    }
}
