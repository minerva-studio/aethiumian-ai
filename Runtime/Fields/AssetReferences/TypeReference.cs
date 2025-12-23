using System;
using System.Linq;
using System.Reflection;
using UnityEngine.Serialization;

namespace Amlos.AI.References
{
    /// <summary>
    /// class that point to reference of a Component type
    /// </summary>
    [Serializable]
    public abstract class TypeReference : IEquatable<TypeReference>
    {
        [FormerlySerializedAs("classFullName")]
        public string fullName = "";
        public string assemblyName = "";

        protected Type referType;



        /// <summary>
        /// The base type this type reference point to
        /// </summary>
        /// <remarks>This returns the parent highest parent class this type reference can point to</remarks>
        public abstract Type BaseType { get; }

        /// <summary>
        /// The type this type reference is point to
        /// </summary>
        public Type ReferType => referType ??= (TryResolve(out referType) ? referType : null);

        /// <summary>
        /// Whether type ref has a target type
        /// </summary>
        public bool HasReferType => ReferType != null;

        /// <summary>
        /// Simple qualified name
        /// </summary>
        public string SimpleQualifiedName => $"{fullName}, {assemblyName}";




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
                fullName = string.Empty;
                assemblyName = string.Empty;
                return true;
            }
            // invalid refer type
            else if (!type.IsSubclassOf(BaseType) && type != BaseType)
            {
                return false;
            }
            else
            {
                fullName = type.FullName ?? type.Name;
                assemblyName = type.Assembly.GetName().Name;
                return true;
            }
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
        /// Try resolve type from self
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public bool TryResolve(out Type type)
        {
            type = null;

            var asm = AppDomain.CurrentDomain.GetAssemblies()
                         .FirstOrDefault(a => string.Equals(a.GetName().Name, assemblyName, StringComparison.Ordinal));
            if (asm != null)
                type = asm.GetType(fullName, throwOnError: false);

            if (type == null)
                type = Type.GetType($"{fullName}, {assemblyName}", throwOnError: false);

            if (type == null)
            {
                try
                {
                    var loaded = Assembly.Load(new AssemblyName(assemblyName));
                    type = loaded.GetType(fullName, throwOnError: false);
                }
                catch { }
            }

            return type != null;
        }

        public Type ResolveOrThrow()
        {
            if (TryResolve(out var t)) return t;
            throw new TypeLoadException($"Cannot resolve type: {fullName}, assembly: {assemblyName}");
        }




        public bool Equals(TypeReference other) =>
            string.Equals(assemblyName, other.assemblyName, StringComparison.Ordinal) &&
            string.Equals(fullName, other.fullName, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is TypeReference o && Equals(o);

        public override int GetHashCode() => ((assemblyName?.GetHashCode() ?? 0) * 397) ^ (fullName?.GetHashCode() ?? 0);

        public override string ToString() => $"{fullName}, {assemblyName}";

        /// <summary>
        /// Implicit convert type reference to type
        /// </summary>
        /// <param name="tr"></param>
        public static implicit operator Type(TypeReference tr) => tr.ReferType;
    }

    [Serializable]
    public class GenericTypeReference : TypeReference
    {
        protected Type baseType;

        /// <summary>
        /// The base type this type reference point to
        /// </summary>
        /// <remarks>This returns the parent highest parent class this type reference can point to</remarks>
        public override Type BaseType => baseType ??= typeof(object);

        /// <summary>
        /// Implicit convert type reference to type
        /// </summary>
        /// <param name="tr"></param>
        public static implicit operator Type(GenericTypeReference tr) => tr.ReferType;

        /// <summary>
        /// Implicit convert type reference to type
        /// </summary>
        /// <param name="type"></param>
        public static implicit operator GenericTypeReference(Type type)
        {
            GenericTypeReference typeReference = new() { baseType = type, };
            typeReference.SetReferType(type);
            return typeReference;
        }

        /// <summary>
        /// Set the base type of the type reference 
        /// </summary>
        /// <param name="type"></param>
        public void SetBaseType(Type type)
        {
            baseType = type;
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
        public override Type BaseType => typeof(T);
    }
}
