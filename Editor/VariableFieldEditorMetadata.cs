using System;
using System.Collections.Generic;
using System.Reflection;
using Aethiumian.AI.Variables;

namespace Aethiumian.AI.Editor
{
    /// <summary>
    /// Resolves editor-only variable type and access metadata for variable fields.
    /// </summary>
    internal static class VariableFieldEditorMetadata
    {
        private sealed class MemberMetadata
        {
            internal IReadOnlyList<VariableType> AllowedTypes { get; }
            internal VariableAccessFlag AccessFlag { get; }

            internal MemberMetadata(IReadOnlyList<VariableType> allowedTypes, VariableAccessFlag accessFlag)
            {
                AllowedTypes = allowedTypes;
                AccessFlag = accessFlag;
            }
        }

        private static readonly Dictionary<MemberInfo, MemberMetadata> MetadataByMember = new();

        /// <summary>Determines whether a field opts into authoring-time type selection.</summary>
        internal static bool IsDynamic(VariableFieldBase field) => field is IDynamicVariableField;

        /// <summary>Gets the cached allowed variable types for a field and reflected member.</summary>
        internal static IReadOnlyList<VariableType> GetAllowedTypes(VariableFieldBase field, MemberInfo member)
        {
            if (!IsDynamic(field))
            {
                return VariableTypeCatalog.GetSingleType(field.Type);
            }

            return GetMetadata(member).AllowedTypes;
        }

        /// <summary>Gets the cached read/write access flags for a reflected member.</summary>
        internal static VariableAccessFlag GetAccessFlag(MemberInfo member) => GetMetadata(member).AccessFlag;

        private static MemberMetadata GetMetadata(MemberInfo member)
        {
            if (MetadataByMember.TryGetValue(member, out MemberMetadata metadata))
            {
                return metadata;
            }

            metadata = new MemberMetadata(
                ResolveAllowedTypes(member),
                ResolveAccessFlag(member));
            MetadataByMember.Add(member, metadata);
            return metadata;
        }

        private static IReadOnlyList<VariableType> ResolveAllowedTypes(MemberInfo member)
        {
            ConstraintAttribute constraint = Attribute.GetCustomAttribute(member, typeof(ConstraintAttribute)) as ConstraintAttribute;
            IReadOnlyList<VariableType> source = constraint == null
                ? VariableTypeCatalog.GetAllVariableTypes()
                : ReadOnlyCopy(constraint.VariableTypes);

            ExcludeAttribute exclude = Attribute.GetCustomAttribute(member, typeof(ExcludeAttribute)) as ExcludeAttribute;
            if (exclude == null)
            {
                return source;
            }

            HashSet<VariableType> excluded = new(exclude.VariableTypes ?? Array.Empty<VariableType>());
            VariableType[] filtered = new VariableType[source.Count];
            int count = 0;
            for (int i = 0; i < source.Count; i++)
            {
                VariableType candidate = source[i];
                if (!excluded.Contains(candidate))
                {
                    filtered[count++] = candidate;
                }
            }

            if (count == filtered.Length)
            {
                return Array.AsReadOnly(filtered);
            }

            Array.Resize(ref filtered, count);
            return Array.AsReadOnly(filtered);
        }

        private static VariableAccessFlag ResolveAccessFlag(MemberInfo member)
        {
            Attribute[] attributes = Attribute.GetCustomAttributes(member, typeof(AccessAttribute));
            VariableAccessFlag result = VariableAccessFlag.None;
            for (int i = 0; i < attributes.Length; i++)
            {
                if (attributes[i] is ReadableAttribute)
                {
                    result |= VariableAccessFlag.Read;
                }
                else if (attributes[i] is WritableAttribute)
                {
                    result |= VariableAccessFlag.Write;
                }
            }

            return result;
        }

        private static IReadOnlyList<VariableType> ReadOnlyCopy(VariableType[] values)
        {
            return Array.AsReadOnly(values == null ? Array.Empty<VariableType>() : (VariableType[])values.Clone());
        }
    }
}
