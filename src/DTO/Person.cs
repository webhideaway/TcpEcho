using ZeroFormatter;

namespace DTO
{
    [ZeroFormattable]
    public struct Person
    {
        public Person(string name, int age)
        {
            Name = name;
            Age = age;
        }

        [Index(0)]
        public readonly string Name;

        [Index(1)]
        public readonly int Age;
    }
}
