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
        /// <summary>
        /// <see cref="UnityEngine.Object"/>
        /// </summary>
        UnityObject,
        /// <summary>
        /// <see cref="object"/>
        /// </summary>
        Generic,
    }
}
