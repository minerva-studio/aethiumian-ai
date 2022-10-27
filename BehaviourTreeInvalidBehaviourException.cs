using System;
using System.Runtime.Serialization;

namespace Amlos.AI
{
    [Serializable]
    public class InvalidBehaviourTreeException : Exception
    {
        public InvalidBehaviourTreeException()
        {
        }

        public InvalidBehaviourTreeException(string message) : base(message)
        {
        }

        public InvalidBehaviourTreeException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected InvalidBehaviourTreeException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}