using System;
using System.Collections.Generic;

namespace Aethiumian.AI.Accessors
{
    /// <summary>
    /// Central helper for value-level deep clone semantics.
    /// </summary>
    public static class Duplicate
    {
        /// <summary>
        /// Duplicates a value according to deep clone rules.
        /// </summary>
        /// <typeparam name="T">The source value type.</typeparam>
        /// <param name="value">The value to duplicate.</param>
        /// <returns>An equivalent duplicate, or the same value when the type is immutable or identity based.</returns>
        public static T Value<T>(T value)
        {
            if (value is null)
            {
                return default;
            }

            Type type = value.GetType();
            if (type.IsValueType || value is string || value is Type)
            {
                return value;
            }

            if (value is UnityEngine.Object)
            {
                return value;
            }

            if (value is IDuplicable duplicable)
            {
                return (T)duplicable.Duplicate();
            }

            throw new InvalidOperationException($"Type {type.FullName} does not support duplicate. Mutable reference types must implement IDuplicable.");
        }

        /// <summary>
        /// Duplicates a list and every supported item in it.
        /// </summary>
        /// <typeparam name="T">The list item type.</typeparam>
        /// <param name="source">The source list.</param>
        /// <returns>A duplicated list, or null when the source is null.</returns>
        public static List<T> List<T>(List<T> source)
        {
            if (source is null)
            {
                return null;
            }

            List<T> result = new(source.Count);
            for (int i = 0; i < source.Count; i++)
            {
                result.Add(Value(source[i]));
            }

            return result;
        }

        /// <summary>
        /// Duplicates an array and every supported item in it.
        /// </summary>
        /// <typeparam name="T">The array item type.</typeparam>
        /// <param name="source">The source array.</param>
        /// <returns>A duplicated array, or null when the source is null.</returns>
        public static T[] Array<T>(T[] source)
        {
            if (source is null)
            {
                return null;
            }

            T[] result = new T[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                result[i] = Value(source[i]);
            }

            return result;
        }
    }
}
