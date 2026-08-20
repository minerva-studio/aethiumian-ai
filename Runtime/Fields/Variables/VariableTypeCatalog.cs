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
        private static readonly IReadOnlyList<VariableType> AllTypes = ReadOnly(new[] {
            VariableType.Int, VariableType.Float, VariableType.String, VariableType.Bool,
            VariableType.Vector2, VariableType.Vector3, VariableType.Vector4,
            VariableType.Generic, VariableType.UnityObject });
        private static readonly IReadOnlyList<VariableType> NodeTypes = ReadOnly(new[] { VariableType.Node });
        private static readonly IReadOnlyList<VariableType> EmptyTypes = Array.Empty<VariableType>();
        private static readonly IReadOnlyList<VariableType> StringTypes = ReadOnly(new[] { VariableType.String, VariableType.Int, VariableType.Generic });
        private static readonly IReadOnlyList<VariableType> IntTypes = ReadOnly(new[] {
            VariableType.String, VariableType.Int, VariableType.Float, VariableType.Bool,
            VariableType.Vector2, VariableType.Vector3, VariableType.Vector4, VariableType.Generic });
        private static readonly IReadOnlyList<VariableType> FloatTypes = IntTypes;
        private static readonly IReadOnlyList<VariableType> BoolTypes = ReadOnly(new[] {
            VariableType.String, VariableType.Bool, VariableType.Float, VariableType.Int,
            VariableType.Vector2, VariableType.Vector3, VariableType.Vector4, VariableType.Generic });
        private static readonly IReadOnlyList<VariableType> VectorTypes = ReadOnly(new[] {
            VariableType.String, VariableType.Bool, VariableType.Vector2,
            VariableType.Vector3, VariableType.Vector4, VariableType.Generic });
        private static readonly IReadOnlyList<VariableType> UnityObjectTypes = ReadOnly(new[] {
            VariableType.String, VariableType.Int, VariableType.Float, VariableType.Bool,
            VariableType.Vector2, VariableType.Vector3, VariableType.Vector4,
            VariableType.UnityObject, VariableType.Generic });

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

        /// <summary>Determines whether source is accepted by target in the current compatibility matrix.</summary>
        public static bool IsCompatible(VariableType source, VariableType target)
        {
            return target switch
            {
                VariableType.Node => source == VariableType.Node,
                VariableType.Invalid => false,
                VariableType.String => source == VariableType.String || source == VariableType.Int || source == VariableType.Generic,
                VariableType.Int or VariableType.Float => source == VariableType.String || source == VariableType.Int || source == VariableType.Float ||
                    source == VariableType.Bool || source == VariableType.Vector2 || source == VariableType.Vector3 || source == VariableType.Vector4 || source == VariableType.Generic,
                VariableType.Bool => source == VariableType.String || source == VariableType.Bool || source == VariableType.Float || source == VariableType.Int ||
                    source == VariableType.Vector2 || source == VariableType.Vector3 || source == VariableType.Vector4 || source == VariableType.Generic,
                VariableType.Vector2 or VariableType.Vector3 or VariableType.Vector4 => source == VariableType.String || source == VariableType.Bool ||
                    source == VariableType.Vector2 || source == VariableType.Vector3 || source == VariableType.Vector4 || source == VariableType.Generic,
                VariableType.UnityObject => source == VariableType.String || source == VariableType.Int || source == VariableType.Float || source == VariableType.Bool ||
                    source == VariableType.Vector2 || source == VariableType.Vector3 || source == VariableType.Vector4 || source == VariableType.UnityObject || source == VariableType.Generic,
                VariableType.Generic => source == VariableType.Int || source == VariableType.Float || source == VariableType.String ||
                    source == VariableType.Bool || source == VariableType.Vector2 || source == VariableType.Vector3 ||
                    source == VariableType.Vector4 || source == VariableType.UnityObject || source == VariableType.Generic,
                _ => false,
            };
        }

        /// <summary>Gets the cached read-only list of source types accepted by target.</summary>
        public static IReadOnlyList<VariableType> GetCompatibleTypes(VariableType target)
        {
            return target switch
            {
                VariableType.Node => NodeTypes,
                VariableType.Invalid => EmptyTypes,
                VariableType.String => StringTypes,
                VariableType.Int => IntTypes,
                VariableType.Float => FloatTypes,
                VariableType.Bool => BoolTypes,
                VariableType.Vector2 or VariableType.Vector3 or VariableType.Vector4 => VectorTypes,
                VariableType.UnityObject => UnityObjectTypes,
                VariableType.Generic => AllTypes,
                _ => EmptyTypes,
            };
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
