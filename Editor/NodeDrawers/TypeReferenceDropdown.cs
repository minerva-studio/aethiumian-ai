using Aethiumian.AI.References;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace Aethiumian.AI.Editor
{
    /// <summary>AdvancedDropdown catalogue for serialized type references.</summary>
    internal sealed class TypeReferenceDropdown : AdvancedDropdown
    {
        private const float MinimumWidth = 320f;
        private const float MinimumHeight = 280f;

        private readonly Action<Type> selectionCallback;
        private readonly Type baseType;

        /// <summary>Initializes a type reference dropdown for the supplied base type.</summary>
        internal TypeReferenceDropdown(Type baseType, Action<Type> selectionCallback)
            : base(new AdvancedDropdownState())
        {
            this.baseType = baseType ?? throw new ArgumentNullException(nameof(baseType));
            this.selectionCallback = selectionCallback;
            minimumSize = new Vector2(MinimumWidth, MinimumHeight);
        }

        /// <inheritdoc />
        protected override AdvancedDropdownItem BuildRoot()
        {
            AdvancedDropdownItem root = new($"Select {baseType.Name}");
            root.AddChild(new TypeReferenceItem("None", null));

            Dictionary<string, AdvancedDropdownItem> folders = new(StringComparer.Ordinal);
            foreach (Type type in TypeReferenceDropdownCatalogue.GetCandidates(baseType))
            {
                AdvancedDropdownItem parent = root;
                string path = string.Empty;
                string[] namespaceSegments = (type.Namespace ?? string.Empty)
                    .Split('.', StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < namespaceSegments.Length; i++)
                {
                    path = path.Length == 0 ? namespaceSegments[i] : $"{path}.{namespaceSegments[i]}";
                    if (!folders.TryGetValue(path, out AdvancedDropdownItem folder))
                    {
                        folder = new AdvancedDropdownItem(namespaceSegments[i]);
                        folders.Add(path, folder);
                        parent.AddChild(folder);
                    }

                    parent = folder;
                }

                parent.AddChild(new TypeReferenceItem(type.Name, type));
            }

            return root;
        }

        /// <inheritdoc />
        protected override void ItemSelected(AdvancedDropdownItem item)
        {
            if (item is TypeReferenceItem typeItem)
            {
                selectionCallback?.Invoke(typeItem.Type);
            }
        }

        private sealed class TypeReferenceItem : AdvancedDropdownItem
        {
            internal TypeReferenceItem(string name, Type type) : base(name)
            {
                Type = type;
            }

            internal Type Type { get; }
        }
    }

    /// <summary>Builds the stable, filtered type catalogue used by TypeReferenceDropdown.</summary>
    internal static class TypeReferenceDropdownCatalogue
    {
        /// <summary>Returns the concrete visible types assignable to the base type.</summary>
        internal static IReadOnlyList<Type> GetCandidates(Type baseType)
        {
            if (baseType == null)
            {
                return Array.Empty<Type>();
            }

            return Enumerable.Repeat(baseType, 1)
                .Concat(TypeCache.GetTypesDerivedFrom(baseType))
                .Where(IsAllowed)
                .Distinct()
                .OrderBy(type => type.Namespace ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ThenBy(type => type.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(type => type.FullName, StringComparer.Ordinal)
                .ToArray();

            bool IsAllowed(Type type)
            {
                return type != null
                    && type.IsVisible
                    && !type.IsGenericTypeDefinition
                    && !string.IsNullOrEmpty(type.FullName)
                    && baseType.IsAssignableFrom(type);
            }
        }
    }
}
