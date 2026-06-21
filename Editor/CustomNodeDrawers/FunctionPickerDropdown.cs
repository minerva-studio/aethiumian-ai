using Amlos.AI.Nodes;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.IMGUI.Controls;

namespace Amlos.AI.Editor
{
    /// <summary>
    /// Add Component-style dropdown for choosing a FunctionCall target.
    /// </summary>
    public sealed class FunctionPickerDropdown : AdvancedDropdown
    {
        private static readonly string[] TopLevelOrder =
        {
            "Arithmetic",
            "Global",
            "Target Script",
            "GameObject",
            "Transform",
            "Object",
            "Other"
        };

        private readonly List<FunctionRegistry.FunctionCandidate> candidates;
        private readonly Action<FunctionRegistry.FunctionCandidate> onSelect;

        public FunctionPickerDropdown(
            AdvancedDropdownState state,
            IEnumerable<FunctionRegistry.FunctionCandidate> candidates,
            Action<FunctionRegistry.FunctionCandidate> onSelect) : base(state)
        {
            this.candidates = candidates
                .Where(candidate => candidate?.Method != null)
                .OrderBy(candidate => GetTopLevelOrder(candidate.Path))
                .ThenBy(candidate => candidate.Path)
                .ThenBy(candidate => candidate.SortKey)
                .ThenBy(candidate => candidate.DisplaySignature)
                .ToList();
            this.onSelect = onSelect;
            minimumSize = new UnityEngine.Vector2(520f, 420f);
        }

        protected override AdvancedDropdownItem BuildRoot()
        {
            AdvancedDropdownItem root = new("Functions");
            Dictionary<string, AdvancedDropdownItem> folders = new(StringComparer.Ordinal);

            foreach (FunctionRegistry.FunctionCandidate candidate in candidates)
            {
                AdvancedDropdownItem parent = GetOrCreateFolder(root, folders, candidate.Path);
                parent.AddChild(new FunctionPickerItem(candidate));
            }

            return root;
        }

        protected override void ItemSelected(AdvancedDropdownItem item)
        {
            if (item is FunctionPickerItem functionItem)
            {
                onSelect?.Invoke(functionItem.Candidate);
            }
        }

        private static AdvancedDropdownItem GetOrCreateFolder(
            AdvancedDropdownItem root,
            Dictionary<string, AdvancedDropdownItem> folders,
            string path)
        {
            AdvancedDropdownItem parent = root;
            string currentPath = string.Empty;
            string[] parts = SplitPath(path);

            foreach (string part in parts)
            {
                currentPath = string.IsNullOrEmpty(currentPath) ? part : $"{currentPath}/{part}";
                if (!folders.TryGetValue(currentPath, out AdvancedDropdownItem folder))
                {
                    folder = new AdvancedDropdownItem(part);
                    folders[currentPath] = folder;
                    parent.AddChild(folder);
                }

                parent = folder;
            }

            return parent;
        }

        private static string[] SplitPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return new[] { "Other" };
            }

            return path
                .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries)
                .DefaultIfEmpty("Other")
                .ToArray();
        }

        private static int GetTopLevelOrder(string path)
        {
            string topLevel = SplitPath(path)[0];
            int index = Array.IndexOf(TopLevelOrder, topLevel);
            return index < 0 ? TopLevelOrder.Length : index;
        }

        private sealed class FunctionPickerItem : AdvancedDropdownItem
        {
            public FunctionRegistry.FunctionCandidate Candidate { get; }

            public FunctionPickerItem(FunctionRegistry.FunctionCandidate candidate)
                : base(GetDisplayName(candidate))
            {
                Candidate = candidate;
            }

            private static string GetDisplayName(FunctionRegistry.FunctionCandidate candidate)
            {
                if (string.IsNullOrEmpty(candidate.DisplayName) || candidate.DisplayName == candidate.Method.Name)
                {
                    return candidate.DisplaySignature;
                }

                return $"{candidate.DisplayName}  {candidate.DisplaySignature}";
            }
        }
    }
}
