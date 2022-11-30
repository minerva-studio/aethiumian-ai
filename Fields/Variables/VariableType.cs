using System;
using UnityEngine;

namespace Amlos.AI
{
    public enum VariableType
    {
        /// <summary>
        /// <see cref="NodeProgress"/>
        /// </summary>
        Node = -2,
        [HideInInspector]
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
            if (type == typeof(Vector2))
            {
                return VariableType.Vector2;
            }
            if (type == typeof(Vector3))
            {
                return VariableType.Vector3;
            }
            if (type == typeof(NodeProgress))
            {
                return VariableType.Node;
            }

            return VariableType.Invalid;
        }
    }
}
