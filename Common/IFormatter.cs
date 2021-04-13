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

        void Serialize<T>(T value, Stream stream);

        T Deserialize<T>(byte[] data);

        T Deserialize<T>(Stream stream);
    }
}
