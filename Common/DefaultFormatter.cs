using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common
{
    public class DefaultFormatter : IFormatter
    {
        public T Deserialize<T>(byte[] data)
        {
            throw new NotImplementedException();
        }

        public T Deserialize<T>(Stream stream)
        {
            throw new NotImplementedException();
        }

        public byte[] Serialize<T>(T value)
        {
            throw new NotImplementedException();
        }

        public void Serialize<T>(T value, Stream stream)
        {
            throw new NotImplementedException();
        }
    }
}
