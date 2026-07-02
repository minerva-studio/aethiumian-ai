using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Aethiumian.AI.Editor
{
    /// <summary>
    /// Local MonoScript cache for AI editor script previews.
    /// </summary>
    internal static class MonoScriptCache
    {
        private static Dictionary<Type, MonoScript> scripts;

        public static MonoScript Get<T>() => Get(typeof(T));

        public static MonoScript Get(Type type)
        {
            if (type == null)
            {
                return null;
            }

            scripts ??= BuildCache();
            return scripts.TryGetValue(type, out MonoScript script) ? script : null;
        }

        private static Dictionary<Type, MonoScript> BuildCache()
        {
            Dictionary<Type, MonoScript> cache = new();
            foreach (MonoScript script in Resources.FindObjectsOfTypeAll<MonoScript>())
            {
                Type type = script.GetClass();
                if (type != null)
                {
                    cache[type] = script;
                }
            }

            return cache;
        }
    }
}
