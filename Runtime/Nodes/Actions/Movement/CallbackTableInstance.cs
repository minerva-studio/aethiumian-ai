using System;
using System.Collections.Generic;
using System.Reflection;

namespace Aethiumian.AI.Nodes
{
    public class CallbackTable
    {
        static Dictionary<string, CallbackTableInstance> methods = new();

        public static void Call<T>(ref T target, string name)
        {
            if (!methods.TryGetValue(name, out var m))
            {
                m = new CallbackTableInstance(name);
                methods[name] = m;
            }
            m.Call(ref target);
        }

    }

    public class CallbackTableInstance
    {
        public readonly string methodName;
        public Dictionary<Type, MethodInfo> table = new Dictionary<Type, MethodInfo>();
        public BindingFlags? flags;

        public CallbackTableInstance(string methodName, BindingFlags? flags = null)
        {
            this.methodName = methodName;
            this.table = new Dictionary<Type, MethodInfo>();
            this.flags = flags;
        }

        public void Call<T>(ref T target)
        {
            if (target is null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            Type key = target.GetType();
            if (!table.TryGetValue(key, out var m))
            {
                if (flags.HasValue) m = key.GetMethod(methodName, flags.Value);
                else m = key.GetMethod(methodName);
                table[key] = m;
            }
            if (m == null) return;
            m.Invoke(target, Array.Empty<object>());
        }
    }
}