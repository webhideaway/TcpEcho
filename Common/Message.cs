using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Common
{
    [Serializable]
    public struct Message
    {
        private Message(
            string id = null,
            string type = null,
            byte[] address = null,
            int port = 0,
            byte[] raw = null)
        {
            Id = id;
            Type = type;
            Address = address;
            Port = port;
            Raw = raw;
        }

        public readonly string Id;
        public readonly string Type;
        public readonly byte[] Address;
        public readonly int Port;
        public readonly byte[] Raw;

        public static Message Create<T>(byte[] raw, IPEndPoint endPoint = null)
        {
            return new Message(
                id: Guid.NewGuid().ToString(),
                type: typeof(T).Name,
                address: endPoint?.Address.GetAddressBytes(),
                port: endPoint == null ? default : endPoint.Port,
                raw: raw
            );
        }
    }
}
