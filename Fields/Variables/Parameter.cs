using System;
using System.Collections.Generic;
using UnityEngine;

namespace Amlos.AI
{
    /// <summary>
    /// a dynamic variable field in the node that has type controlled by the script
    /// </summary> 
    [Serializable]
    public class Parameter : VariableField
    {
        public Parameter() : base()
        {
        }

        public static object[] ToValueArray(TreeNode node, List<Parameter> parameters)
        {
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
                    Debug.Log(item.type);
                    arr[i] = VariableUtility.ImplicitConversion(item.type, item.Value);
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
