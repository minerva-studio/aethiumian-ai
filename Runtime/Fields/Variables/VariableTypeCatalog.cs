using Aethiumian.AI.Nodes;
using Aethiumian.AI.References;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace Aethiumian.AI.Variables
{
    /// <summary>Provides canonical CLR type classification and variable compatibility rules.</summary>
    public static class VariableTypeCatalog
    {
        private static readonly IReadOnlyList<VariableType> AllVariableTypes = ReadOnly(new[] {
            VariableType.Node, VariableType.Invalid, VariableType.String, VariableType.Int,
            VariableType.Float, VariableType.Bool, VariableType.Vector2, VariableType.Vector3,
            VariableType.Vector4, VariableType.UnityObject, VariableType.Generic });
        private static readonly IReadOnlyDictionary<VariableType, IReadOnlyList<VariableType>> SingleTypeLists = CreateSingleTypeLists();
        private static readonly IReadOnlyDictionary<VariableType, IReadOnlyList<VariableType>> CompatibleTypeLists = CreateCompatibleTypeLists();

        /// <summary>Gets the cached variable type for a CLR type.</summary>
        public static VariableType Of<T>() => TypeCache<T>.Value;

        /// <summary>Classifies a CLR type using the canonical variable type rules.</summary>
        public static VariableType Of(Type type)
        {
            if (type is null) return VariableType.Generic;
            if (type == typeof(NodeProgress) || type == typeof(System.Threading.CancellationToken) || typeof(TreeNode).IsAssignableFrom(type))
                return VariableType.Node;
            if (type.IsEnum || type == typeof(int) || type == typeof(uint) || type == typeof(LayerMask))
                return VariableType.Int;
            if (type == typeof(float)) return VariableType.Float;
            if (type == typeof(string)) return VariableType.String;
            if (type == typeof(bool)) return VariableType.Bool;
            if (type == typeof(Vector2) || type == typeof(Vector2Int)) return VariableType.Vector2;
            if (type == typeof(Vector3) || type == typeof(Vector3Int)) return VariableType.Vector3;
            if (type == typeof(Vector4) || type == typeof(Color)) return VariableType.Vector4;
            if (typeof(UnityEngine.Object).IsAssignableFrom(type)) return VariableType.UnityObject;
            return VariableType.Generic;
        }

        /// <summary>Classifies a runtime value, returning Generic for null.</summary>
        public static VariableType Of(object value) => value is null ? VariableType.Generic : Of(value.GetType());

        /// <summary>Gets all declared variable types in canonical enum order.</summary>
        public static IReadOnlyList<VariableType> GetAllVariableTypes() => AllVariableTypes;

        /// <summary>Gets the cached single-item list for a variable type.</summary>
        public static IReadOnlyList<VariableType> GetSingleType(VariableType type) => SingleTypeLists[type];

        /// <summary>Checks source-to-target conversion support using the canonical CLR types for the variable categories.</summary>
        /// <remarks>Node is reserved for injected control values and only matches Node. Invalid never matches. Query ConversionRegistry with concrete CLR types when object, enum, or script-member types are known.</remarks>
        public static bool IsCompatible(VariableType source, VariableType target)
        {
            if (source == VariableType.Invalid || target == VariableType.Invalid) return false;
            if (source == VariableType.Node || target == VariableType.Node) return source == target;
            Type sourceType = VariableUtility.GetType(source);
            Type targetType = VariableUtility.GetType(target);
            return sourceType != null && targetType != null && ConversionRegistry.CanConvert(sourceType, targetType);
        }

        /// <summary>Checks the conversion directions needed to access a variable through a field's value type.</summary>
        /// <param name="variableType">The variable's actual declared CLR type.</param>
        /// <param name="fieldType">The CLR type read or written by the field's consumer.</param>
        /// <param name="access">Read requires variable-to-field conversion; Write requires field-to-variable conversion. All and None require both directions.</param>
        /// <returns>Whether all requested conversion rules exist. Member getter/setter availability and individual value validity are separate requirements.</returns>
        /// <exception cref="ArgumentNullException">Either type is null.</exception>
        /// <exception cref="ArgumentException">Either type cannot be used as a generic conversion argument.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Access contains unknown flags.</exception>
        public static bool SupportsAccess(Type variableType, Type fieldType, VariableAccessFlag access = VariableAccessFlag.All)
        {
            if (variableType == null) throw new ArgumentNullException(nameof(variableType));
            if (fieldType == null) throw new ArgumentNullException(nameof(fieldType));
            if ((access & ~VariableAccessFlag.All) != 0) throw new ArgumentOutOfRangeException(nameof(access));
            if (access == VariableAccessFlag.None) access = VariableAccessFlag.All;

            return ((access & VariableAccessFlag.Read) == 0 || ConversionRegistry.CanConvert(variableType, fieldType))
                && ((access & VariableAccessFlag.Write) == 0 || ConversionRegistry.CanConvert(fieldType, variableType));
        }

        /// <summary>Gets the cached read-only source categories derived from the runtime conversion rules for a target category.</summary>
        public static IReadOnlyList<VariableType> GetCompatibleTypes(VariableType target)
        {
            return CompatibleTypeLists.TryGetValue(target, out IReadOnlyList<VariableType> sources) ? sources : Array.Empty<VariableType>();
        }

        /// <summary>Builds immutable source lists for every category from the shared source-to-target rules during initialization.</summary>
        private static IReadOnlyDictionary<VariableType, IReadOnlyList<VariableType>> CreateCompatibleTypeLists()
        {
            Dictionary<VariableType, IReadOnlyList<VariableType>> result = new();
            foreach (VariableType target in AllVariableTypes)
            {
                List<VariableType> sources = new();
                foreach (VariableType source in AllVariableTypes)
                {
                    if (IsCompatible(source, target)) sources.Add(source);
                }
                result.Add(target, sources.AsReadOnly());
            }
            return new ReadOnlyDictionary<VariableType, IReadOnlyList<VariableType>>(result);
        }

        private static IReadOnlyList<VariableType> ReadOnly(VariableType[] values) => new ReadOnlyCollection<VariableType>(values);

        private static IReadOnlyDictionary<VariableType, IReadOnlyList<VariableType>> CreateSingleTypeLists()
        {
            Dictionary<VariableType, IReadOnlyList<VariableType>> result = new();
            for (int i = 0; i < AllVariableTypes.Count; i++)
            {
                VariableType type = AllVariableTypes[i];
                result.Add(type, ReadOnly(new[] { type }));
            }

            return result;
        }

        private static class TypeCache<T>
        {
            internal static readonly VariableType Value = Of(typeof(T));
        }
    }
}
