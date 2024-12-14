using System;
using System.Collections.Generic;

namespace LPS.Domain.LPSSession
{
    public class Payload
    {
        public enum PayloadType
        {
            Raw,
            Multipart,
            Binary
        }

        public PayloadType Type { get; private set; }
        public string RawValue { get; private set; }
        public Dictionary<string, object> MultipartData { get; private set; }
        public string FilePath { get; private set; }

        private Payload(PayloadType type)
        {
            Type = type;
        }

        public static Payload CreateRaw(string rawValue)
        {
            return new Payload(PayloadType.Raw)
            {
                RawValue = rawValue
            };
        }

        public static Payload CreateMultipart(Dictionary<string, object> multipartData)
        {
            return new Payload(PayloadType.Multipart)
            {
                MultipartData = multipartData
            };
        }

        public static Payload CreateBinary(byte[] binaryData)
        {
            throw new NotImplementedException();
        }
    }
}
