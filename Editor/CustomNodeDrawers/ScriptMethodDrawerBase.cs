using System.Linq;
using System.Reflection;
using UnityEditor;

namespace Amlos.AI.Editor
{
    public abstract class ScriptMethodDrawerBase : NodeDrawerBase
    {
        protected int selected;

        public bool IsParameterValid(ParameterInfo info)
        {
            if (info.ParameterType == typeof(string))
                return true;
            if (info.ParameterType == typeof(int))
                return true;
            if (info.ParameterType == typeof(float))
                return true;
            if (info.ParameterType == typeof(bool))
                return true;
            if (info.ParameterType == typeof(NodeProgress))
                return true;
            return false;
        }

        protected string[] GetOptions()
        {
            return tree.targetScript.GetClass()
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => !m.IsSpecialName && !m.ContainsGenericParameters && IsValidMethod(m))
                .Select(m => m.Name)
                .ToArray();
        }

        protected virtual bool IsValidMethod(MethodInfo m)
        {
            ParameterInfo[] parameterInfos = m.GetParameters();
            if (parameterInfos.Length == 0)
            {
                return true;
            }

            for (int i = 0; i < parameterInfos.Length; i++)
            {
                ParameterInfo item = parameterInfos[i];
                VariableType variableType = item.ParameterType.GetVariableType();
                if (variableType == VariableType.Invalid) return false;
                if (variableType == VariableType.Node && (i != 0 || item.ParameterType != typeof(NodeProgress)))
                {
                    return false;
                }
            }

            return true;
        }
    }
}