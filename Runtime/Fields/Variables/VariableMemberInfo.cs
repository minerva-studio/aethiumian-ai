using System;
using System.Reflection;

namespace Aethiumian.AI.Variables
{
    /// <summary>Describes the value type and supported access directions of a script variable member.</summary>
    public readonly struct VariableMemberInfo
    {
        /// <summary>Gets the reflected member described by this result.</summary>
        public MemberInfo Member { get; }

        /// <summary>Gets the CLR value type read from or written to the member.</summary>
        public Type Type { get; }

        /// <summary>Gets whether the member can supply a value without arguments.</summary>
        public bool CanRead { get; }

        /// <summary>Gets whether the member can accept a value.</summary>
        public bool CanWrite { get; }

        /// <summary>Creates a resolved member contract; the default value is not a resolved contract.</summary>
        private VariableMemberInfo(MemberInfo member, Type type, bool canRead, bool canWrite)
        {
            Member = member;
            Type = type;
            CanRead = canRead;
            CanWrite = canWrite;
        }

        /// <summary>Resolves a field, non-indexed property, or getter/setter-like method as a variable member.</summary>
        /// <param name="member">The member to inspect, including non-public property accessors.</param>
        /// <param name="info">The resolved contract on success; default on failure. Use it only when this method returns true.</param>
        /// <returns>True for supported variable members; false for missing or unsupported members.</returns>
        /// <remarks>A method reads through its zero-argument return value or writes through its single parameter; a write method's return value is ignored.</remarks>
        public static bool TryResolve(MemberInfo member, out VariableMemberInfo info)
        {
            info = default;
            Type type;
            bool canRead;
            bool canWrite;
            switch (member)
            {
                case FieldInfo field:
                    type = field.FieldType;
                    canRead = true;
                    canWrite = !field.IsInitOnly && !field.IsLiteral;
                    break;
                case PropertyInfo property:
                    if (property.GetIndexParameters().Length != 0) return false;
                    type = property.PropertyType;
                    canRead = property.GetGetMethod(true) != null;
                    MethodInfo setter = property.GetSetMethod(true);
                    canWrite = setter != null && setter.GetParameters().Length == 1
                        && VariableUtility.IsSupportedAccessorType(setter.GetParameters()[0].ParameterType);
                    if (!canRead && !canWrite) return false;
                    break;
                case MethodInfo method:
                    if (method.ContainsGenericParameters) return false;
                    ParameterInfo[] parameters = method.GetParameters();
                    canRead = parameters.Length == 0;
                    canWrite = parameters.Length == 1;
                    if (!canRead && !canWrite) return false;
                    type = canRead ? method.ReturnType : parameters[0].ParameterType;
                    break;
                default:
                    return false;
            }

            if (!VariableUtility.IsSupportedAccessorType(type)) return false;
            info = new VariableMemberInfo(member, type, canRead, canWrite);
            return true;
        }
    }
}
