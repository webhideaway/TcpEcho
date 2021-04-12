using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common
{
    public interface IFormatter
    {
        byte[] Serialize<T>(T value);

        T Deserialize<T>(byte[] data);
    }
}
