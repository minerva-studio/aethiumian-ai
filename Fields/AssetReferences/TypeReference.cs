using System;

namespace Amlos.AI.References
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

        /// <summary>
        /// The base type this type reference point to
        /// </summary>
        /// <remarks>This returns the parent highest parent class this type reference can point to</remarks>
        public virtual Type BaseType => baseType ??= typeof(object);

        /// <summary>
        /// The type this type reference is point to
        /// </summary>
        public Type ReferType => referType ??= string.IsNullOrEmpty(assemblyFullName) ? null : Type.GetType(assemblyFullName);

        public bool HasReferType => ReferType != null;

        /// <summary>
        /// Set the type reference
        /// </summary>
        /// <param name="type"></param>
        public void SetReferType(Type type)
        {
            referType = type;
            if (type == null)
            {
                assemblyFullName = string.Empty;
            }
            else
            {
                classFullName = type.FullName;
                assemblyFullName = type.AssemblyQualifiedName;
            }
        }

        public void SetBaseType(Type type)
        {
            baseType = type;
        }

        public bool IsSubclassOf(Type type)
        {
            return ReferType?.IsSubclassOf(type) == true;
        }

        /// <summary>
        /// Implicit convert type reference to type
        /// </summary>
        /// <param name="cr"></param>
        public static implicit operator Type(TypeReference cr)
        {
            return cr.ReferType;
        }

        /// <summary>
        /// Implicit convert type reference to type
        /// </summary>
        /// <param name="cr"></param>
        public static implicit operator TypeReference(Type type)
        {
            TypeReference typeReference = new() { baseType = type, };
            typeReference.SetReferType(type);
            return typeReference;
        }
    }

    /// <summary>
    /// A generic type reference
    /// </summary>
    /// <typeparam name="T">the highest parent class this type reference refer to.</typeparam>
    /// <remarks>Note: if you want to select any type, use <see cref="TypeReference"/> with no generic parameter</remarks>
    [Serializable]
    public class TypeReference<T> : TypeReference
    {
        public override Type BaseType => baseType ??= typeof(T);
    }
}
