using ZeroFormatter;

namespace DTO
{
    [ZeroFormattable]
    public struct Car
    {
        public Car(string reg, int age)
        {
            Reg = reg;
            Age = age;
        }

        [Index(0)]
        public readonly string Reg;

        [Index(1)]
        public readonly int Age;
    }
}
