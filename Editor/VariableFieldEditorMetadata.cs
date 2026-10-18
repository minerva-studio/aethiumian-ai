using System;
using System.Collections.Generic;
using System.Reflection;
using Aethiumian.AI.Nodes;
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

        /// <summary>Gets the cached visible variable types for the reflected member.</summary>
        /// <remarks>The member's constraint/exclude attributes are intersected with the category its authored node-field role requires. Compatibility with a field's consumer type is resolved separately through <see cref="VariableTypeCatalog.SupportsAccess"/>.</remarks>
        internal static IReadOnlyList<VariableType> GetAllowedTypes(MemberInfo member) => GetMetadata(member).AllowedTypes;

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
                VariableUtility.GetAccessFlag(member));
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
            IReadOnlyList<VariableType> allowed = source;
            if (exclude != null)
            {
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

                if (count != filtered.Length)
                {
                    Array.Resize(ref filtered, count);
                }

                allowed = Array.AsReadOnly(filtered);
            }

            VariableType? requiredCategory = GetRequiredSourceCategory(member);
            if (requiredCategory == null)
            {
                return allowed;
            }

            for (int i = 0; i < allowed.Count; i++)
            {
                if (allowed[i] == requiredCategory.Value)
                {
                    return allowed.Count == 1 ? allowed : ReadOnlyCopy(new[] { requiredCategory.Value });
                }
            }

            return Array.Empty<VariableType>();
        }

        /// <summary>Returns the only category an authored node field can consume, or null when the field accepts any category.</summary>
        /// <remarks>Cooldown and Throttle timers must be Timer Floats, while Countdown updates only ordinary Float variables. Visibility must match the selector's candidate rule so the type menu cannot offer a category the field rejects.</remarks>
        private static VariableType? GetRequiredSourceCategory(MemberInfo member)
        {
            if (member?.Name == "timer" && (member.DeclaringType == typeof(Cooldown) || member.DeclaringType == typeof(Throttle)))
            {
                return VariableType.Float;
            }

            if (member?.Name == "updatingVariable" && member.DeclaringType == typeof(Countdown))
            {
                return VariableType.Float;
            }

            return null;
        }

        private static IReadOnlyList<VariableType> ReadOnlyCopy(VariableType[] values)
        {
            return Array.AsReadOnly(values == null ? Array.Empty<VariableType>() : (VariableType[])values.Clone());
        }
    }
}
