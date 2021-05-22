using System;
using System.Linq;
using System.Net;
using ZeroFormatter;

namespace ZeroPipeline.Interfaces
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

        public static Message Create<TRequest>(byte[] rawData, IPEndPoint callbackEndPoint)
        {
            return Create(Guid.NewGuid().ToString(), typeof(TRequest), rawData, callbackEndPoint);
        }

        public static Message Create(string id, Type type, byte[] rawData = null, IPEndPoint callbackEndPoint = null)
        {
            return new Message(
                id: id,
                typeName: $"{type.FullName}, {type.Assembly.FullName}",
                callbackAddress: callbackEndPoint?.Address.GetAddressBytes(),
                callbackPort: callbackEndPoint?.Port ?? 0,
                rawData: rawData
            );
        }

        public static byte[] BOM => "<BOM>".ToCharArray().Select(_ => Convert.ToByte(_)).ToArray();

        public static byte[] EOM => "<EOM>".ToCharArray().Select(_ => Convert.ToByte(_)).ToArray();
    }
}
