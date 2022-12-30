using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Amlos.AI
{
    /// <summary>
    /// Interface for all method callers
    /// </summary>
    public interface IMethodCaller
    {
        List<Parameter> Parameters { get; set; }
        VariableReference Result { get; }
        string MethodName { get; set; }
    }

    public interface ITypeReference
    {
        TypeReference TypeReference { get; }
    }

    /// <summary>
    /// Node that require a component
    /// </summary>
    public interface IComponentCaller : ITypeReference
    {
        bool GetComponent { get; set; }
        VariableReference Component { get; }
    }

    public interface IObjectCaller : ITypeReference
    {
        VariableReference Object { get; }
    }

    /// <summary>
    /// A generic method caller that require type reference as method's declaring type
    /// </summary>
    public interface IGenericMethodCaller : IMethodCaller, ITypeReference
    {
    }

    /// <summary>
    /// A generic method caller that call to a component
    /// </summary>
    public interface IComponentMethodCaller : IGenericMethodCaller, IMethodCaller, IComponentCaller
    {

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
