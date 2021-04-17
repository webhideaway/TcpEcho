using System;

namespace Common
{
    public interface IFormatter
    {
        byte[] Serialize<T>(T value);

        byte[] Serialize(Type type, object value);

        T Deserialize<T>(byte[] data);

        object Deserialize(Type type, byte[] data);
    }
}
