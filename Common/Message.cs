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
            string typeName = null,
            byte[] callbackAddress = null,
            int callbackPort = 0,
            byte[] rawData = null)
        {
            Id = id;
            TypeName = typeName;
            CallbackAddress = callbackAddress;
            CallbackPort = callbackPort;
            RawData = rawData;
        }

        [Index(0)]
        public readonly string Id;

        [Index(1)]
        public readonly string TypeName;

        [Index(2)]
        public readonly byte[] CallbackAddress;

        [Index(3)]
        public readonly int CallbackPort;

        [Index(4)]
        public readonly byte[] RawData;

        public static Message Create<TRequest>(byte[] rawData, IPEndPoint callbackEndPoint = null)
        {
            return Create(typeof(TRequest), rawData, callbackEndPoint);
        }

        internal static Message Create(Type type, byte[] rawData, IPEndPoint callbackEndPoint = null)
        {
            return new Message(
                id: Guid.NewGuid().ToString(),
                typeName: type.FullName,
                callbackAddress: callbackEndPoint?.Address.GetAddressBytes(),
                callbackPort: callbackEndPoint?.Port ?? 0,
                rawData: rawData
            );
        }

        public static byte EOM => Convert.ToByte(ConsoleKey.Escape);
    }
}
