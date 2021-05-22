using DTO;
using System;
using System.Net;
using System.Threading.Tasks;

namespace Server
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var localEndPoint = new IPEndPoint(IPAddress.Loopback, 1212);

            Console.WriteLine("Server listening for requests on local end point: {0}", localEndPoint);

            using var server = new ZeroPipeline.Server(localEndPoint);

            server.RegisterHandler<Person, Person>(person =>
            {
                return new Person(person.Name.ToLowerInvariant(), person.Age * -1);
            });

            server.RegisterHandler<Car, Car>(car =>
            {
                return new Car(car.Brand.ToLowerInvariant(), car.Age * -1);
            });

            await server.ListenAsync(input: (id, request, count) =>
            {
                if (request.GetType().IsAssignableFrom(typeof(Person)))
                {
                    var person = (Person)request;
                    Console.WriteLine($"PERSON [REQUEST] [Name = {person.Name}, Age = {person.Age}]");
                }
                if (request.GetType().IsAssignableFrom(typeof(Car)))
                {
                    var car = (Car)request;
                    Console.WriteLine($"CAR [REQUEST] [Brand = {car.Brand}, Age = {car.Age}]");
                }
            });
        }
    }
}
