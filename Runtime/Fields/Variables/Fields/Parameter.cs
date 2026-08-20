using Aethiumian.AI.Nodes;
using Aethiumian.AI.References;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using UnityEngine;

namespace Aethiumian.AI.Variables
{
    /// <summary>
    /// a dynamic variable field in the node that has type controlled by the script
    /// </summary> 
    [Serializable]
    public class Parameter : DynamicVariableFieldBase
    {
        [SerializeField] protected VariableType type;

        /// <summary>Gets or sets the reflected CLR type used to render and convert this parameter.</summary>
        public Type ParameterObjectType { get; set; }
        public override bool IsDynamicType => true;
        public override Type FieldObjectType => ParameterObjectType;
        public override VariableType Type => type;

        public Parameter() { }
        public Parameter(VariableType type) => this.type = type;
        public Parameter(object value)
        {
            type = VariableUtility.GetVariableType(value?.GetType());
            if (value is Enum)
            {
                ParameterObjectType = value.GetType();
                type = VariableType.Int;
            }
            SetConstantValue(value is Enum ? Convert.ToInt32(value) : value);
        }
        public Parameter(Type type)
        {
            ParameterObjectType = type;
            this.type = VariableUtility.GetVariableType(type);
        }

        /// <summary>Sets the parameter type selected by the reflected method signature.</summary>
        public void ForceSetConstantType(VariableType variableType)
        {
            if (type == variableType) return;
            type = variableType;
            ResetConstantValue();
        }

        public static object[] ToValueArray(TreeNode node, MethodInfo methodInfo, List<Parameter> parameters, Func<CancellationTokenSource> cancellation = null)
        {
            var methodParameters = methodInfo.GetParameters();
            var arr = new object[parameters.Count];
            for (int i = 0; i < parameters.Count; i++)
            {
                Parameter item = parameters[i];
                if (item.type == VariableType.Node)
                {
                    Type parameterType = methodParameters[i].ParameterType;
                    if (parameterType == typeof(NodeProgress) && node is Nodes.Action action)
                        arr[i] = new NodeProgress(action);
                    else if (parameterType == typeof(CancellationToken))
                        arr[i] = cancellation?.Invoke()?.Token ?? default;
                    else
                    {
                        Debug.LogError($"Unable to handle argument on {node.name}({node.uuid}) {parameterType.FullName}");
                        throw new InvalidCastException();
                    }
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
        public override void SetRuntimeReference(RuntimeVariable variable)
        {
            var currType = type;
            base.SetRuntimeReference(variable);
            type = currType;
        }

    }

}
