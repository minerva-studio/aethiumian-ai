using System;
using UnityEngine;

namespace Amlos.AI
{
    public enum VariableType
    {
        /// <summary>
        /// <see cref="NodeProgress"/>
        /// </summary>
        [HideInInspector]
        [InspectorName(null)]
        Node = -2,
        [HideInInspector]
        [InspectorName(null)]
        Invalid = -1,
        /// <summary>
        /// <see cref="string"/>
        /// </summary>
        String,
        /// <summary>
        /// <see cref="int"/>
        /// </summary>
        Int,
        /// <summary>
        /// <see cref="float"/>
        /// </summary>
        Float,
        /// <summary>
        /// <see cref="bool"/>
        /// </summary>
        Bool,
        /// <summary>
        /// <see cref="UnityEngine.Vector2"/>
        /// </summary>
        Vector2,
        /// <summary>
        /// <see cref="UnityEngine.Vector3"/>
        /// </summary>
        Vector3,
        [HideInInspector]
        [InspectorName(null)]
        Vector4,
    }

    public static class VariableTypeExtensions
    {
        public static VariableType GetVariableType(this Type type)
        {
            if (type == typeof(int))
            {
                return VariableType.Int;
            }
            if (type == typeof(float))
            {
                return VariableType.Float;
            }
            if (type == typeof(string))
            {
                return VariableType.String;
            }
            if (type == typeof(bool))
            {
                return VariableType.Bool;
            }
            if (type == typeof(Vector2) || type == typeof(Vector2Int))
            {
                return VariableType.Vector2;
            }
            if (type == typeof(Vector3) || type == typeof(Vector3Int))
            {
                return VariableType.Vector3;
            }
            if (type == typeof(NodeProgress))
            {
                return VariableType.Node;
            }

            return VariableType.Invalid;
        }

        public static VariableType[] GetCompatibleTypes(VariableType type)
        {
            switch (type)
            {
                case VariableType.Node:
                    return Array(VariableType.Node);
                case VariableType.Invalid:
                    return Array();
                case VariableType.String:
                    return Array(VariableType.String, VariableType.Float, VariableType.Bool, VariableType.Int, VariableType.Vector2, VariableType.Vector3);
                case VariableType.Int:
                    return Array(VariableType.Int, VariableType.Float);
                case VariableType.Float:
                    return Array(VariableType.Int, VariableType.Float);
                case VariableType.Bool:
                    return Array(VariableType.Bool, VariableType.Float, VariableType.Int, VariableType.String, VariableType.Vector2, VariableType.Vector3);
                case VariableType.Vector2:
                    return Array(VariableType.Vector3, VariableType.Vector2);
                case VariableType.Vector3:
                    return Array(VariableType.Vector3, VariableType.Vector2);
                default:
                    break;
            }

            return Array();

            static VariableType[] Array(params VariableType[] variableTypes)
            {
                return variableTypes;
            }
        }
    }
}
