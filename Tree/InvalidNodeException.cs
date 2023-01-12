using System;
using System.Runtime.Serialization;

namespace Amlos.AI
{
    [Serializable]
    public class InvalidNodeException : Exception
    {
        public InvalidNodeException() { }
        public InvalidNodeException(string message) : base(message) { }
        public InvalidNodeException(string message, Exception inner) : base(message, inner) { }
        protected InvalidNodeException(
          SerializationInfo info,
          StreamingContext context) : base(info, context) { }

        
        
        public static InvalidNodeException VariableIsRequired(string varName)
        {
            return new InvalidNodeException($"Variable \"{varName}\" is Required");
        }

        public static InvalidNodeException InvalidValue(string varName, object value)
        {
            return new InvalidNodeException($"Variable \"{varName}\" has invalid value: {value}");
        }
    }
}