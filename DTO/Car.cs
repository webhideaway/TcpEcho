using ZeroFormatter;

namespace DTO
{
    [ZeroFormattable]
    public struct Car
    {
        public Car(string brand, int age)
        {
            Brand = brand;
            Age = age;
        }

        [Index(0)]
        public readonly string Brand;

        [Index(1)]
        public readonly int Age;
    }
}
