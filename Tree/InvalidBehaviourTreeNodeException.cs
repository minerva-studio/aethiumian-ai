using System.Runtime.Serialization;
using System;

namespace Amlos.AI
{
    public class InvalidBehaviourTreeNodeException : InvalidBehaviourTreeException
    {
        public InvalidBehaviourTreeNodeException()
        {
        }

        public InvalidBehaviourTreeNodeException(string message) : base(message)
        {
        }

        public InvalidBehaviourTreeNodeException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected InvalidBehaviourTreeNodeException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}