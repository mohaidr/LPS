using System;
using System.Runtime.Serialization;

namespace LPS.UI.Common
{
    [Serializable]
    internal class LPSValidationException : Exception
    {
        public LPSValidationException()
        {
        }

        public LPSValidationException(string message) : base(message)
        {
        }

        public LPSValidationException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected LPSValidationException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}