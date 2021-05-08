using System;
using System.IO;

namespace Common
{
    public interface IFormatter : IDisposable
    {
        byte[] Serialize<T>(T value);

        byte[] Serialize(Type type, object value);

        void Serialize<T>(Stream stream, T value);

        T Deserialize<T>(byte[] data);

        object Deserialize(Type type, byte[] data);

        T Deserialize<T>(Stream stream);
    }
}
