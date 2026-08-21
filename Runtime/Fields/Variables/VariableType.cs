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
        /// <summary>Gets the number of components represented by a numeric or vector variable type.</summary>
        /// <returns>One through four for supported types; otherwise, zero.</returns>
        public static int ComponentCount(this VariableType type)
        {
            return type switch
            {
                VariableType.Int => 1,
                VariableType.Float => 1,
                VariableType.Bool => 1,
                VariableType.Vector2 => 2,
                VariableType.Vector3 => 3,
                VariableType.Vector4 => 4,
                _ => 0,
            };
        }
    }
}
