using Amlos.AI.Nodes;
using System;

namespace Amlos.AI
{
    /// <summary>
    /// Returning behaviour tree state
    /// </summary>
    public class NodeReturnException : Exception
    {
        readonly State returnValue;

        public State ReturnValue => returnValue;


        public NodeReturnException(State returnValue)
        {
            this.returnValue = returnValue;
        }

    }
}