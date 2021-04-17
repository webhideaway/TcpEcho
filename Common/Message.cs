using System;
using System.Net;
using ZeroFormatter;

namespace Common
{
    [ZeroFormattable]
    public struct Message
    {
        public Message(
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

        [Index(0)]
        public readonly string Id;

        [Index(1)]
        public readonly string Type;

        [Index(2)]
        public readonly byte[] Address;

        [Index(3)]
        public readonly int Port;

        [Index(4)]
        public readonly byte[] Raw;

        public static Message Create<T>(byte[] raw, IPEndPoint endPoint = null)
        {
            return new Message(
                id: Guid.NewGuid().ToString(),
                type: typeof(T).FullName,
                address: endPoint?.Address.GetAddressBytes(),
                port: endPoint == null ? default : endPoint.Port,
                raw: raw
            );
        }
    }
}
