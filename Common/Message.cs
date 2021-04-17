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
            string requestType = null,
            string responseType = null,
            byte[] callbackAddress = null,
            int callbackPort = 0,
            byte[] rawData = null)
        {
            Id = id;
            RequestType = requestType;
            ResponseType = responseType;
            CallbackAddress = callbackAddress;
            CallbackPort = callbackPort;
            RawData = rawData;
        }

        [Index(0)]
        public readonly string Id;

        [Index(1)]
        public readonly string RequestType;

        [Index(2)]
        public readonly string ResponseType;

        [Index(3)]
        public readonly byte[] CallbackAddress;

        [Index(4)]
        public readonly int CallbackPort;

        [Index(5)]
        public readonly byte[] RawData;

        public static Message Create<TRequest>(byte[] rawData)
        {
            return new Message(
                id: Guid.NewGuid().ToString(),
                requestType: typeof(TRequest).FullName,
                rawData: rawData
            );
        }

        public static Message Create<TRequest, TResponse>(byte[] rawData, IPEndPoint callbackEndPoint)
        {
            return new Message(
                id: Guid.NewGuid().ToString(),
                requestType: typeof(TRequest).FullName,
                responseType: typeof(TResponse).FullName,
                callbackAddress: callbackEndPoint.Address.GetAddressBytes(),
                callbackPort: callbackEndPoint.Port,
                rawData: rawData
            );
        }
    }
}
