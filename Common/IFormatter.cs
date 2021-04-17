using System;

namespace Common
{
    public interface IFormatter
    {
        byte[] Serialize<T>(T value);

        T Deserialize<T>(byte[] data);

        object Deserialize(Type type, byte[] data);
    }
}
