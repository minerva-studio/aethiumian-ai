#nullable enable 
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Amlos.AI.Utils
{
    internal static class UnitySerialization
    {
        /// <summary>
        /// Enumerate instance fields that Unity would serialize (approximation of official rules),
        /// with inheritance and [SerializeReference] support.
        /// </summary>
        public static IEnumerable<FieldInfo> GetUnitySerializedFields(Type t)
        {
            // Walk the type hierarchy so we include private [SerializeField] fields declared on base types.
            for (var cur = t; cur != null && cur != typeof(object); cur = cur.BaseType)
            {
                const BindingFlags flags =
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

                foreach (var f in cur.GetFields(flags))
                {
                    if (f.IsStatic) continue;
                    if (f.IsInitOnly) continue; // Unity does not serialize readonly fields
                    if (f.GetCustomAttribute<NonSerializedAttribute>() != null) continue;

                    // Public (and not [NonSerialized]) OR private/protected with [SerializeField]
                    bool isPublicSerialized = f.IsPublic;
                    bool hasSerializeField = f.GetCustomAttribute(SerializeFieldAttributeType) != null;

                    // Honor [SerializeReference] — managed reference fields are serialized polymorphically
                    bool hasSerializeReference = f.GetCustomAttribute(SerializeReferenceAttributeType) != null;

                    if (hasSerializeReference)
                    {
                        yield return f;
                        continue;
                    }

                    if (!(isPublicSerialized || hasSerializeField)) continue;

                    // Otherwise apply standard Unity type rules (primitives, UnityEngine.Object, [Serializable], List<T>/T[])
                    if (!IsUnitySerializableType(f.FieldType)) continue;

                    yield return f;
                }
            }
        }

        static bool IsUnitySerializableType(Type t)
        {
            if (t.IsPrimitive || t.IsEnum || t == typeof(string) || t == typeof(decimal)) return true;

#if UNITY_5_3_OR_NEWER
            if (typeof(UnityEngine.Object).IsAssignableFrom(t)) return true;
#endif
            if (t.IsArray)
                return t.GetArrayRank() == 1 && IsUnitySerializableType(t.GetElementType()!);

            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>))
                return IsUnitySerializableType(t.GetGenericArguments()[0]);

            return HasSerializableAttribute(t);
        }

        static bool HasSerializableAttribute(Type t)
            => t.IsValueType || t.GetCustomAttribute(SerializableAttributeType) != null;

        static readonly Type SerializableAttributeType = typeof(SerializableAttribute);

        static readonly Type? SerializeFieldAttributeType =
#if UNITY_5_3_OR_NEWER
            typeof(UnityEngine.SerializeField);
#else
            Type.GetType("UnityEngine.SerializeField, UnityEngine") ?? null;
#endif

        static readonly Type? SerializeReferenceAttributeType =
#if UNITY_5_3_OR_NEWER
            typeof(UnityEngine.SerializeReference);
#else
            Type.GetType("UnityEngine.SerializeReference, UnityEngine") ?? null;
#endif
    }
}
