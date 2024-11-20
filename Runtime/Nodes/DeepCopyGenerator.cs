using Amlos.AI.References;
using Amlos.AI.Variables;
using Minerva.Module;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace Amlos.AI.Nodes
{
    public static class DeepCopy
    {
        static Dictionary<Type, DeepCopyGenerator> generators;

        static DeepCopy()
        {
            generators = new();
            generators[typeof(UUID)] = new UUIDDeepCopyGenerator();
        }

        public static object Copy(object obj)
        {
            var result = Copy(obj, 0);
            return result;
        }

        public static object Copy(object obj, int level)
        {
            //if (obj is ICloneable cloneable) return cloneable.Clone();
            if (obj is string) return obj;
            if (obj is UnityEngine.Object) return obj;
            if (obj == null) return null;
            Type key = obj.GetType();
            if (!generators.TryGetValue(key, out var generator))
            {
                generator = generators[key] = new DeepCopyGenerator(key);
            }
            return generator.DeepCopy(obj, level);
        }


        class UUIDDeepCopyGenerator : DeepCopyGenerator
        {
            public UUIDDeepCopyGenerator() : base(typeof(UUID)) { }


            public override object DeepCopy(object source, int level)
            {
                if (source is not UUID uuid) return UUID.Empty;
                return uuid;
            }
        }


        class DeepCopyGenerator
        {
            private Type type;
            private FieldInfo[] memberInfos;

            public DeepCopyGenerator(Type type)
            {
                this.type = type;
            }

            private void Init()
            {
                memberInfos = GetAllField(type).ToArray();
            }

            public virtual object DeepCopy(object source, int level)
            {
                if (source == null)
                    return default;

                if (memberInfos == null) Init();

                if (type.IsArray)
                {
                    Array oldArray = (Array)source;
                    Array newArr = Array.CreateInstance(type.GetElementType(), oldArray.Length);
                    for (int i = 0; i < oldArray.Length; i++)
                    {
                        newArr.SetValue(Copy(oldArray.GetValue(i), level + 1), i);
                    }
                    return newArr;
                }
                else if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
                {
                    IList oldList = (IList)source;
                    var newList = Activator.CreateInstance(type) as IList;
                    for (int i = 0; i < oldList.Count; i++)
                    {
                        newList.Add(Copy(oldList[i], level + 1));
                    }
                    return newList;
                }

                var newInstance = Activator.CreateInstance(type);
                foreach (var field in memberInfos)
                {
                    var oldValue = field.GetValue(source);
                    //if (oldValue is VariableBase || source is VariableBase)
                    //    Debug.Log(oldValue);
                    object newValue = null;
                    if (oldValue is UnityEngine.Object)
                    {
                        newValue = oldValue;
                    }
                    else if (field.FieldType.IsPrimitive || field.FieldType.IsEnum || field.FieldType == typeof(string))
                    {
                        newValue = oldValue;
                    }
                    else if (oldValue is not null)
                    {
                        newValue = Copy(oldValue, level + 1);
                    }
                    field.SetValue(newInstance, newValue);
                }
                return newInstance;
            }


            static IEnumerable<FieldInfo> GetAllField(Type type)
            {
                foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    if (!field.IsPublic && !Attribute.IsDefined(field, typeof(SerializeField))) continue;
                    yield return field;
                }
                if (type.BaseType != typeof(object))
                {
                    foreach (FieldInfo field in GetAllField(type.BaseType))
                    {
                        yield return field;
                    }
                }
            }
        }
    }
}
