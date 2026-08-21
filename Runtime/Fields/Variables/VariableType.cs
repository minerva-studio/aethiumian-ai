using Aethiumian.AI.References;
using UnityEngine;

namespace Aethiumian.AI
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
        /// <summary>
        /// <see cref="UnityEngine.Vector4"/> or <see cref="Color"/>
        /// </summary>
        Vector4,
        /// <summary>
        /// <see cref="UnityEngine.Object"/>
        /// </summary>
        UnityObject,
        /// <summary>
        /// <see cref="object"/>
        /// </summary>
        Generic,
    }

    public static class VariableTypeExtensions
    {
        /// <summary>
        /// Determines whether a variable type belongs to the componentwise numeric domain.
        /// </summary>
        public static bool IsComponentwiseType(this VariableType type)
        {
            return type == VariableType.Int || type == VariableType.Float ||
                type == VariableType.Vector2 || type == VariableType.Vector3 || type == VariableType.Vector4;
        }
    }
}
