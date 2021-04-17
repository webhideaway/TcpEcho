using System;
using ZeroFormatter;

namespace Common
{
    public class DefaultFormatter : IFormatter
    {
        public T Deserialize<T>(byte[] data)
        {
            return ZeroFormatterSerializer.Deserialize<T>(data);
        }

        public object Deserialize(Type type, byte[] data)
        {
            return ZeroFormatterSerializer.NonGeneric.Deserialize(type, data);
        }

        public byte[] Serialize<T>(T value)
        {
            return ZeroFormatterSerializer.Serialize<T>(value);
        }
    }
}
