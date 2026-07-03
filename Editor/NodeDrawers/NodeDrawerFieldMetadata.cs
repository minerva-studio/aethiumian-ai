using Aethiumian.AI.Attributes;
using System;
using System.Reflection;
using UnityEditor;

namespace Aethiumian.AI.Editor
{
    /// <summary>
    /// Evaluates AI node field metadata used by node drawers only.
    /// </summary>
    internal static class NodeDrawerFieldMetadata
    {
        public static bool ShouldDraw(object owner, FieldInfo field)
        {
            return ShouldDraw(owner, field, includeConditionalHidden: false);
        }

        public static bool ShouldDraw(object owner, FieldInfo field, bool includeConditionalHidden)
        {
            if (field == null)
            {
                return true;
            }

            if (includeConditionalHidden || !Attribute.IsDefined(field, typeof(DisplayIfAttribute), inherit: true))
            {
                return true;
            }

            return ConditionalFieldAttribute.IsTrue(owner, field);
        }

        public static bool IsReadOnly(FieldInfo field)
        {
            return field != null && Attribute.IsDefined(field, typeof(ReadOnlyAttribute), inherit: true);
        }

        public static FieldInfo GetField(SerializedProperty property)
        {
            return property?.GetAIMemberInfo() as FieldInfo;
        }
    }
}
