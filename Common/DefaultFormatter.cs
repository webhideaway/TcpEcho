using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;

namespace Common
{
    public class DefaultFormatter : IFormatter
    {
        private BinaryFormatter _binaryFormatter = new BinaryFormatter();

        public T Deserialize<T>(byte[] data)
        {
            using (MemoryStream memoryStream = new MemoryStream(data))
            {
                memoryStream.Position = 0;
                return (T)_binaryFormatter.Deserialize(memoryStream);
            }
        }

        public object Deserialize(Type type, byte[] data)
        {
            using (MemoryStream memoryStream = new MemoryStream(data))
            {
                memoryStream.Position = 0;
                var value = _binaryFormatter.Deserialize(memoryStream);
                return Convert.ChangeType(value, type);
            }
        }

        public byte[] Serialize<T>(T value)
        {
            using (MemoryStream memoryStream = new MemoryStream())
            {
                _binaryFormatter.Serialize(memoryStream, value);
                memoryStream.Position = 0;
                return memoryStream.ToArray();
            }
        }
    }
}
