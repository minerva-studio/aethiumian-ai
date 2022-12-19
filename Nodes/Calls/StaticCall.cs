using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Amlos.AI
{

    [Serializable]
    public sealed class StaticCall : Call, IMethodCaller
    {
        public TypeReference type;
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
                var methods = type.ReferType.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
                var method = methods.Where(m => m.Name == MethodName && ParameterMatches(m, parameters)).FirstOrDefault();
                ret = method.Invoke(null, Parameter.ToValueArray(this, Parameters));
                Debug.Log(ret);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Debug.LogException(new ArithmeticException("Method " + MethodName + $" in class {type.ReferType.Name} cannot be invoke!"));
                End(false);
                return;
            }

            if (Result.HasRuntimeReference)
            {
                Result.Value = ret;
            }

            //no return
            if (ret is null)
            {
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

        private bool ParameterMatches(MethodInfo m, List<Parameter> parameters)
        {
            ParameterInfo[] array = m.GetParameters();
            for (int i = 0; i < array.Length; i++)
            {
                ParameterInfo item = array[i];
                if (!VariableUtility.GetCompatibleTypes(VariableUtility.GetVariableType(item.ParameterType)).Contains(parameters[i].Type))
                {
                    return false;
                }
            }
            return true;
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