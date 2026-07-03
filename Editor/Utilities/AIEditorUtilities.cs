using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;

namespace Aethiumian.AI.Editor
{
    /// <summary>
    /// Local serialized property reflection helpers for AI editor drawers.
    /// </summary>
    internal static class AIEditorSerializedPropertyExtensions
    {
        private const BindingFlags MemberBinding = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;
        private static readonly Regex ArrayElementRegex = new(@"\GArray\.data\[(\d+)\]", RegexOptions.Compiled);

        public static object GetAIValue(this SerializedProperty property)
        {
            if (property == null)
            {
                return null;
            }

            if (!property.isArray)
            {
                try
                {
                    return property.boxedValue;
                }
                catch
                {
                    // Some Unity property types do not expose boxedValue; fall back to reflection.
                }
            }

            return GetAIValueByReflection(property);
        }

        public static MemberInfo GetAIMemberInfo(this SerializedProperty property)
        {
            if (property == null)
            {
                return null;
            }

            string propertyPath = property.propertyPath;
            object value = property.serializedObject.targetObject;
            object lastValue = value;
            PropertyPathComponent token = default;
            PropertyPathComponent lastToken;

            int index = 0;
            while (true)
            {
                lastToken = token;
                if (!NextPathComponent(propertyPath, ref index, out token))
                {
                    break;
                }

                lastValue = value;
                value = GetPathComponentValue(value, token);
            }

            return GetPathComponentInfo(lastValue, lastToken);
        }

        private static object GetAIValueByReflection(SerializedProperty property)
        {
            string propertyPath = property.propertyPath;
            object value = property.serializedObject.targetObject;
            int index = 0;
            while (NextPathComponent(propertyPath, ref index, out PropertyPathComponent token))
            {
                value = GetPathComponentValue(value, token);
            }

            return value;
        }

        private static bool NextPathComponent(string propertyPath, ref int index, out PropertyPathComponent component)
        {
            component = default;
            if (index >= propertyPath.Length)
            {
                return false;
            }

            Match arrayElementMatch = ArrayElementRegex.Match(propertyPath, index);
            if (arrayElementMatch.Success)
            {
                index += arrayElementMatch.Length + 1;
                component.elementIndex = int.Parse(arrayElementMatch.Groups[1].Value);
                return true;
            }

            int dot = propertyPath.IndexOf('.', index);
            if (dot == -1)
            {
                component.propertyName = propertyPath.Substring(index);
                index = propertyPath.Length;
            }
            else
            {
                component.propertyName = propertyPath.Substring(index, dot - index);
                index = dot + 1;
            }

            return true;
        }

        private static MemberInfo GetPathComponentInfo(object container, PropertyPathComponent component)
        {
            return component.propertyName == null ? null : GetMemberInfo(container, component.propertyName);
        }

        private static object GetPathComponentValue(object container, PropertyPathComponent component)
        {
            if (container == null)
            {
                return null;
            }

            return component.propertyName == null
                ? ((IList)container)[component.elementIndex]
                : GetMemberValue(container, component.propertyName);
        }

        private static MemberInfo GetMemberInfo(object container, string name)
        {
            return container == null ? null : GetInfo(container.GetType(), name);
        }

        private static object GetMemberValue(object container, string name)
        {
            if (container == null)
            {
                return null;
            }

            MemberInfo member = GetInfo(container.GetType(), name);
            if (member == null || Attribute.IsDefined(member, typeof(ObsoleteAttribute)))
            {
                return null;
            }

            return member switch
            {
                FieldInfo field => field.GetValue(container),
                PropertyInfo property => property.GetValue(container),
                _ => null,
            };
        }

        private static MemberInfo GetInfo(Type parentType, string name)
        {
            FieldInfo fieldInfo = parentType.GetField(name, MemberBinding);
            if (fieldInfo != null)
            {
                return fieldInfo;
            }

            PropertyInfo propertyInfo = parentType.GetProperty(name, MemberBinding);
            if (propertyInfo != null)
            {
                return propertyInfo;
            }

            return parentType.BaseType != null ? GetInfo(parentType.BaseType, name) : null;
        }

        private struct PropertyPathComponent
        {
            public string propertyName;
            public int elementIndex;
        }
    }
}
