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

            server.RegisterHandler<Person, Person>(request =>
            {
                var response = new Person(request.Name.ToLowerInvariant(), request.Age * -1);
                Console.WriteLine($"PERSON [RESPONSE] [Name = {response.Name}, Age = {response.Age}]");
                return response;

            });

            server.RegisterHandler<Car, Car>(request =>
            {
                var response = new Car(request.Brand.ToLowerInvariant(), request.Age * -1);
                Console.WriteLine($"CAR [RESPONSE] [Brand = {response.Brand}, Age = {response.Age}]");
                return response;
            });

            await server.ListenAsync(input: request =>
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
