using System;
using System.Collections.Generic;

namespace Amlos.AI
{
    [Serializable]
    public abstract class Call : TreeNode
    {
        public override void Initialize()
        {
        }
    }

    public interface IMethodCaller
    {
        List<Parameter> Parameters { get; set; }
        VariableReference Result { get; set; }
        string MethodName { get; set; }
    }
}