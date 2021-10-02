using System;
using System.Runtime.Serialization;

namespace ZamboniLib
{
    class ZamboniException : Exception
    {
        public ZamboniException() : base()
        {
        }

        public ZamboniException(string message) : base(message)
        {
        }

        public ZamboniException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected ZamboniException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}