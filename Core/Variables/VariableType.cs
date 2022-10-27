using System;
using UnityEngine;

namespace Amlos.AI
{
    public enum VariableType
    {
        [HideInInspector]
        Invalid = -1,
        String,
        Int,
        Float,
        Bool,

        Vector2,
        Vector3
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

            return VariableType.Invalid;
        }
    }
}
