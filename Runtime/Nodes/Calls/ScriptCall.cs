using Amlos.AI.Variables;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Amlos.AI.Nodes
{
    /// <summary>
    /// Call a method in the script once and return,
    /// the method allows to be called is:
    /// 
    /// <code>
    /// void MethodName(NodeProgress progress)  //inside method return
    /// void MethodName()                       //return true
    /// bool MethodName()                       //return method return
    /// </code> 
    /// 
    /// </summary>
    /// <remarks>
    /// Use <see cref="ComponentAction"/> instead
    /// </remarks>
    [NodeTip("Call a method in the script once and return")]
    [Serializable]
    [Obsolete]
    [DoNotRelease]
    public sealed class ScriptCall : Call, IMethodCaller
    {
        public string methodName;
        public List<Parameter> parameters;
        public VariableReference result;

        public List<Parameter> Parameters { get => parameters; set => parameters = value; }
        public VariableReference Result { get => result; set => result = value; }
        public string MethodName { get => methodName; set => methodName = value; }

        public override State Execute()
        {
            object ret;

            var method = behaviourTree.Script.GetType().GetMethod(methodName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            ret = method.Invoke(behaviourTree.Script, Parameter.ToValueArray(this, method, parameters));

            if (result.HasValue) result.SetValue(ret);
            //no return
            if (ret is null)
            {
                Debug.Log("method no return value");
                return State.Success;
            }
            else if (ret is bool b)
            {
                return StateOf(b);
            }
            else return State.Success;
        }

        public override void Initialize()
        {
            MethodCallers.InitializeParameters(behaviourTree, this);
        }
    }
}