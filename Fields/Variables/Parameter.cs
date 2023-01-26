using Amlos.AI.References;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Amlos.AI.Variables
{
    /// <summary>
    /// a dynamic variable field in the node that has type controlled by the script
    /// </summary> 
    [Serializable]
    public class Parameter : VariableField
    {
        public Type ParameterObjectType { get; set; }
        public override Type ObjectType => ParameterObjectType;

        public Parameter() : base() { }
        public Parameter(VariableType type) : base(type) { }
        public Parameter(object value) : base(value)
        {
            if (value is Enum)
            {
                ParameterObjectType = value.GetType();
            }
        }
        public Parameter(Type type) : base()
        {
            ParameterObjectType = type;
            base.type = VariableUtility.GetVariableType(type);
        }

        public static object[] ToValueArray(TreeNode node, MethodInfo methodInfo, List<Parameter> parameters)
        {
            var methodParameters = methodInfo.GetParameters();
            var arr = new object[parameters.Count];
            for (int i = 0; i < parameters.Count; i++)
            {
                Parameter item = parameters[i];
                if (item.type == VariableType.Node)
                {
                    arr[i] = new NodeProgress(node);
                }
                else
                {
                    Type parameterType = methodParameters[i].ParameterType;
                    arr[i] = VariableUtility.ImplicitConversion(parameterType, item.Value);
                }
            }
            return arr;
        }

        /// <summary>
        /// set the reference in constructing <see cref="BehaviourTree"/>
        /// </summary>
        /// <param name="variable"></param>
        public override void SetRuntimeReference(Variable variable)
        {
            var currType = type;
            base.SetRuntimeReference(variable);
            type = currType;
        }
    }

}
