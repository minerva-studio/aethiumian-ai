using Amlos.AI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Amlos.AI
{
    public interface IMethodCaller
    {
        List<Parameter> Parameters { get; set; }
        VariableReference Result { get; set; }
        string MethodName { get; set; }
    }

    public interface IComponentMethodCaller : IGenericMethodCaller, IMethodCaller
    {
        bool GetComponent { get; set; }
        VariableReference Component { get; set; }
    }

    public interface IGenericMethodCaller : IMethodCaller
    {
        TypeReference TypeReference { get; }
    }

    public static class MethodCallers
    {
        public static bool ParameterMatches(MethodInfo m, List<Parameter> parameters)
        {
            ParameterInfo[] array = m.GetParameters();
            for (int i = 0; i < array.Length; i++)
            {
                ParameterInfo item = array[i];
                VariableType paramVariableType = VariableUtility.GetVariableType(item.ParameterType);
                if (!VariableUtility.GetCompatibleTypes(paramVariableType).Contains(parameters[i].Type))
                {
                    return false;
                }
            }
            return true;
        }


        public static void InitializeParameters(BehaviourTree behaviourTree, IMethodCaller methodCaller)
        {
            for (int i = 0; i < methodCaller.Parameters.Count; i++)
            {
                Parameter item = methodCaller.Parameters[i];
                methodCaller.Parameters[i] = (Parameter)item.Clone();
                if (!methodCaller.Parameters[i].IsConstant)
                {
                    bool hasVar = behaviourTree.Variables.TryGetValue(methodCaller.Parameters[i].UUID, out Variable variable);
                    if (hasVar) methodCaller.Parameters[i].SetRuntimeReference(variable);
                    else methodCaller.Parameters[i].SetRuntimeReference(null);
                }
            }
        }


    }
}
