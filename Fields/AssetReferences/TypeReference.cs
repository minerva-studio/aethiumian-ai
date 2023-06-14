using System;
using UnityEngine;

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
        /// <returns>Whether refer type can be used in the type reference</returns>
        public bool SetReferType(Type type)
        {
            referType = type;
            if (type == null)
            {
                assemblyFullName = string.Empty;
                return true;
            }
            // invalid refer type
            else if (!type.IsSubclassOf(BaseType) && type != BaseType)
            {
                return false;
            }
            else
            {
                classFullName = type.FullName;
                assemblyFullName = type.AssemblyQualifiedName;
                return true;
            }
        }

        /// <summary>
        /// Set the base type of the type reference
        /// <br/>
        /// Noted that this method does not work with <see cref="TypeReference{T}"/>
        /// </summary>
        /// <param name="type"></param>
        public virtual void SetBaseType(Type type)
        {
            baseType = type;
        }

        /// <summary>
        /// Check whether current representing type is the subclass of given type
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public bool IsSubclassOf(Type type)
        {
            return ReferType?.IsSubclassOf(type) == true;
        }


        /// <summary>
        /// Check whether base type is the subclass of given type
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public bool IsBaseTypeSubclassOf(Type type)
        {
            return BaseType?.IsSubclassOf(type) == true;
        }

        /// <summary>
        /// Implicit convert type reference to type
        /// </summary>
        /// <param name="tr"></param>
        public static implicit operator Type(TypeReference tr)
        {
            return tr.ReferType;
        }

        /// <summary>
        /// Implicit convert type reference to type
        /// </summary>
        /// <param name="type"></param>
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

        /// <summary>
        /// Set the base type of the type reference
        /// <br/>
        /// Noted that this method does not work with <see cref="TypeReference{T}"/>
        /// </summary>
        /// <param name="type"></param>
        public override void SetBaseType(Type type)
        {
            Debug.LogError("Cannot set base type to fixed base type Type Reference");
            baseType = typeof(T);
        }
    }
}
