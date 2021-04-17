using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common
{
    public interface IFormatter
    {
        byte[] Serialize<T>(T value);

        T Deserialize<T>(byte[] data);

        object Deserialize(Type type, byte[] data);
    }
}
