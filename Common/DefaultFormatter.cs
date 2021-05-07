using System;
using System.IO;
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

        public T Deserialize<T>(Stream stream)
        {
            return ZeroFormatterSerializer.Deserialize<T>(stream);
        }

        public byte[] Serialize<T>(T value)
        {
            return ZeroFormatterSerializer.Serialize<T>(value);
        }

        public byte[] Serialize(Type type, object value)
        {
            return ZeroFormatterSerializer.NonGeneric.Serialize(type, value);
        }

        public void Serialize<T>(Stream stream, T value)
        {
            ZeroFormatterSerializer.Serialize<T>(stream, value);
        }
    }
}
