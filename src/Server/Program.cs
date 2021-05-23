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
                if (request.Age < 18) throw new Exception($"Under aged person {request.Name}");

                var response = new Person(request.Name.ToLowerInvariant(), request.Age * -1);
                Console.WriteLine($"PERSON [RESPONSE] [Name = {response.Name}, Age = {response.Age}]");
                return response;

            });

            server.RegisterHandler<Car, Car>(request =>
            {
                if (request.Age < 3) throw new Exception($"Under aged car {request.Brand}");

                var response = new Car(request.Brand.ToLowerInvariant(), request.Age * -1);
                Console.WriteLine($"CAR [RESPONSE] [Brand = {response.Brand}, Age = {response.Age}]");
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
                    Console.WriteLine($"CAR [REQUEST] [Brand = {car.Brand}, Age = {car.Age}]");
                }
            });
        }
    }
}
