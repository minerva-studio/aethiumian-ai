using System;
using System.Reflection;

namespace Amlos.AI
{
    /// <summary>
    /// class that point to reference of a Component type
    /// </summary>
    [Serializable]
    public class TypeReference
    {
        public string classFullName = "";
        public string assemblyFullName = "";
        protected Type baseType;
        protected Type referType;

        //public Func<Type, bool> TypeFilter;

        public virtual Type BaseType => baseType ??= typeof(object);
        public Type ReferType => referType ??= string.IsNullOrEmpty(assemblyFullName) ? null : Type.GetType(assemblyFullName);

        public void SetType(Type type)
        {
            referType = type;
            assemblyFullName = type == null ? string.Empty : type.AssemblyQualifiedName;
        }

        public static implicit operator Type(TypeReference cr)
        {
            return cr.ReferType;
        }
    }


    [Serializable]
    public class TypeReference<T> : TypeReference
    {
        public override Type BaseType => baseType ??= typeof(T);
    }
}
