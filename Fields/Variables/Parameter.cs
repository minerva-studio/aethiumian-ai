using System;
using System.Collections.Generic;

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
                if (item.type == VariableType.Invalid)
                {
                    arr[i] = new NodeProgress(node);
                }
                else
                {
                    arr[i] = item.Value;
                }
            }
            return arr;
        }
    }

}
