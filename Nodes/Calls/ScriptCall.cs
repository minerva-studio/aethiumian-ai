using System;
using System.Collections.Generic;
using System.ComponentModel;
using UnityEngine;

namespace Amlos.AI
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
    [NodeTip("Call a method in the script once and return")]
    [Serializable]
    public sealed class ScriptCall : Call, IMethodCaller
    {
        public string methodName;
        public List<Parameter> parameters;
        public VariableReference result;

        public List<Parameter> Parameters { get => parameters; set => parameters = value; }
        public VariableReference Result { get => result; set => result = value; }
        public string MethodName { get => methodName; set => methodName = value; }

        public override void Execute()
        {
            object ret;
            try
            {
                var method = behaviourTree.Script.GetType().GetMethod(methodName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                ret = method.Invoke(behaviourTree.Script, Parameter.ToValueArray(this, parameters));
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Debug.LogException(new ArithmeticException("Method " + methodName + $" in script {behaviourTree.Script.GetType().Name} cannot be invoke!"));
                End(false);
                return;
            }

            if (result.HasRuntimeValue) result.Value = ret;
            //no return
            if (ret is null)
            {
                Debug.Log("method no return value");
                End(true);
                return;
            }
            else if (ret is bool b)
            {
                End(b);
                return;
            }
            else End(true);
        }

        public override void Initialize()
        {
            for (int i = 0; i < parameters.Count; i++)
            {
                Parameter item = parameters[i];
                parameters[i] = (Parameter)item.Clone();
                if (!parameters[i].IsConstant)
                {
                    bool hasVar = behaviourTree.Variables.TryGetValue(parameters[i].UUID, out Variable variable);
                    if (hasVar) parameters[i].SetRuntimeReference(variable);
                    else parameters[i].SetRuntimeReference(null);
                }
            }
        }
    }
}