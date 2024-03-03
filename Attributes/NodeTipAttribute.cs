using System;
using System.Collections.Generic;
using UnityEditor;

namespace Amlos.AI
{
    /// <summary>
    /// An attribute that allow ai editor to display tooltip for the type of node
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class NodeTipAttribute : Attribute
    {
        readonly static Dictionary<Type, string> tips = new();
        readonly string tip;

        static NodeTipAttribute()
        {
#if UNITY_EDITOR 
            foreach (var type in TypeCache.GetTypesWithAttribute<NodeTipAttribute>())
            {
                var tip = (Attribute.GetCustomAttribute(type, typeof(NodeTipAttribute)) as NodeTipAttribute).Tip;
                NodeTipAttribute.AddEntry(type, tip);
            }
#endif
        }
        /// <summary>
        /// the tip
        /// </summary>
        /// <param name="tip"></param>
        public NodeTipAttribute(string tip)
        {
            this.tip = tip;
        }

        public string Tip
        {
            get { return tip; }
        }

        public static void AddEntry(Type type, string tip)
        {
            tips[type] = tip;
        }

        public static string GetEntry(Type type)
        {
            if (tips.TryGetValue(type, out string tip))
            {
                return tip;
            }
            return string.Empty;
        }
    }
}