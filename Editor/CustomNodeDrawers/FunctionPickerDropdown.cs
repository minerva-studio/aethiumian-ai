using Aethiumian.AI.Nodes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace Aethiumian.AI.Editor
{
    public sealed class FunctionPickerState : AdvancedDropdownState
    {
        private static readonly Predicate<MethodInfo> DefaultMethodFilter = FunctionRegistry.IsValidCallMethod;

        [NonSerialized] private int registryVersion = FunctionRegistry.CacheVersion;
        [NonSerialized] private Type targetScriptType;
        [NonSerialized] private Type objectReceiverType;
        [NonSerialized] private Predicate<MethodInfo> methodFilter = DefaultMethodFilter;
        [NonSerialized] private AdvancedDropdownItem globalSection;
        [NonSerialized] private AdvancedDropdownItem arithmeticSection;
        [NonSerialized] private AdvancedDropdownItem targetScriptSection;
        [NonSerialized] private AdvancedDropdownItem gameObjectSection;
        [NonSerialized] private AdvancedDropdownItem transformSection;
        [NonSerialized] private AdvancedDropdownItem objectSection;

        public Type TargetScriptType => targetScriptType;
        public Type ObjectReceiverType => objectReceiverType;
        public Predicate<MethodInfo> MethodFilter => methodFilter ?? DefaultMethodFilter;

        public void SetContext(Type targetScriptType, Type objectReceiverType, Predicate<MethodInfo> methodFilter)
        {
            methodFilter ??= DefaultMethodFilter;
            int currentRegistryVersion = FunctionRegistry.CacheVersion;
            bool registryChanged = registryVersion != currentRegistryVersion;
            bool filterChanged = !Equals(MethodFilter, methodFilter);

            // Section caches mirror the picker context: global invalidation is rare, target/object invalidation is local.
            if (registryChanged || filterChanged)
            {
                ClearCachedSections();
            }
            else
            {
                if (this.targetScriptType != targetScriptType)
                {
                    targetScriptSection = null;
                }

                if (this.objectReceiverType != objectReceiverType)
                {
                    objectSection = null;
                }
            }

            registryVersion = currentRegistryVersion;
            this.targetScriptType = targetScriptType;
            this.objectReceiverType = objectReceiverType;
            this.methodFilter = methodFilter;
        }

        public bool Matches(MethodInfo method)
        {
            return MethodFilter(method);
        }

        public void EnsureRegistryVersion()
        {
            int currentRegistryVersion = FunctionRegistry.CacheVersion;
            if (registryVersion == currentRegistryVersion)
            {
                return;
            }

            ClearCachedSections();
            registryVersion = currentRegistryVersion;
        }

        public AdvancedDropdownItem GetOrBuildGlobalSection(Func<AdvancedDropdownItem> buildSection)
        {
            EnsureRegistryVersion();
            return GetOrBuildSection(ref globalSection, buildSection);
        }

        public AdvancedDropdownItem GetOrBuildArithmeticSection(Func<AdvancedDropdownItem> buildSection)
        {
            EnsureRegistryVersion();
            return GetOrBuildSection(ref arithmeticSection, buildSection);
        }

        public AdvancedDropdownItem GetOrBuildTargetScriptSection(Func<AdvancedDropdownItem> buildSection)
        {
            EnsureRegistryVersion();
            return GetOrBuildSection(ref targetScriptSection, buildSection);
        }

        public AdvancedDropdownItem GetOrBuildGameObjectSection(Func<AdvancedDropdownItem> buildSection)
        {
            EnsureRegistryVersion();
            return GetOrBuildSection(ref gameObjectSection, buildSection);
        }

        public AdvancedDropdownItem GetOrBuildTransformSection(Func<AdvancedDropdownItem> buildSection)
        {
            EnsureRegistryVersion();
            return GetOrBuildSection(ref transformSection, buildSection);
        }

        public AdvancedDropdownItem GetOrBuildObjectSection(Func<AdvancedDropdownItem> buildSection)
        {
            EnsureRegistryVersion();
            return GetOrBuildSection(ref objectSection, buildSection);
        }

        private void ClearCachedSections()
        {
            globalSection = null;
            arithmeticSection = null;
            targetScriptSection = null;
            gameObjectSection = null;
            transformSection = null;
            objectSection = null;
        }

        private static AdvancedDropdownItem GetOrBuildSection(ref AdvancedDropdownItem section, Func<AdvancedDropdownItem> buildSection)
        {
            section ??= buildSection();
            return section;
        }
    }

    /// <summary>
    /// Add Component-style dropdown for choosing a FunctionCall target.
    /// </summary>
    public sealed class FunctionPickerDropdown : AdvancedDropdown
    {
        private const BindingFlags ContextMethodFlags = BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.FlattenHierarchy;

        private readonly FunctionPickerState pickerState;
        private readonly Action<FunctionRegistry.FunctionCandidate> onSelect;

        public FunctionPickerDropdown(FunctionPickerState state, Action<FunctionRegistry.FunctionCandidate> onSelect) : base(state)
        {
            pickerState = state ?? throw new ArgumentNullException(nameof(state));
            this.onSelect = onSelect;
            minimumSize = new Vector2(520f, 420f);
        }

        protected override AdvancedDropdownItem BuildRoot()
        {
            pickerState.EnsureRegistryVersion();
            AdvancedDropdownItem root = new("Functions");

            // Unity asks the data source to reload on each Show; cached sections keep stable method trees reusable.
            AddSection(root, pickerState.GetOrBuildArithmeticSection(BuildArithmeticSection));
            AddSection(root, pickerState.GetOrBuildGlobalSection(BuildGlobalSection));
            AddSection(root, pickerState.GetOrBuildTargetScriptSection(BuildTargetScriptSection));
            AddSection(root, pickerState.GetOrBuildGameObjectSection(BuildGameObjectSection));
            AddSection(root, pickerState.GetOrBuildTransformSection(BuildTransformSection));
            AddSection(root, pickerState.GetOrBuildObjectSection(BuildObjectSection));

            return root;
        }

        protected override void ItemSelected(AdvancedDropdownItem item)
        {
            if (item is FunctionPickerItem functionItem)
            {
                onSelect?.Invoke(functionItem.Candidate);
            }
        }

        private AdvancedDropdownItem BuildGlobalSection()
        {
            return BuildSection("Global", FunctionRegistry.GetCustomCandidates(pickerState.MethodFilter));
        }

        private AdvancedDropdownItem BuildArithmeticSection()
        {
            return BuildSection("Arithmetic", FunctionRegistry.GetArithmeticFunctions().Where(candidate => pickerState.Matches(candidate.Method)));
        }

        private AdvancedDropdownItem BuildTargetScriptSection()
        {
            return BuildSection(
                "Target Script",
                FunctionRegistry.GetMethodCandidates(
                    pickerState.TargetScriptType,
                    ContextMethodFlags,
                    "Target Script",
                    FunctionRegistry.ReceiverAssignment.TargetScript,
                    includeUnregisteredFolder: true,
                    pickerState.MethodFilter));
        }

        private AdvancedDropdownItem BuildGameObjectSection()
        {
            return BuildSection(
                "GameObject",
                FunctionRegistry.GetMethodCandidates(
                    typeof(GameObject),
                    ContextMethodFlags,
                    "GameObject",
                    FunctionRegistry.ReceiverAssignment.GameObject,
                    includeUnregisteredFolder: false,
                    pickerState.MethodFilter));
        }

        private AdvancedDropdownItem BuildTransformSection()
        {
            return BuildSection(
                "Transform",
                FunctionRegistry.GetMethodCandidates(
                    typeof(Transform),
                    ContextMethodFlags,
                    "Transform",
                    FunctionRegistry.ReceiverAssignment.Transform,
                    includeUnregisteredFolder: false,
                    pickerState.MethodFilter));
        }

        private AdvancedDropdownItem BuildObjectSection()
        {
            return BuildSection(
                "Object",
                FunctionRegistry.GetMethodCandidates(
                    pickerState.ObjectReceiverType,
                    ContextMethodFlags,
                    "Object",
                    FunctionRegistry.ReceiverAssignment.Preserve,
                    includeUnregisteredFolder: true,
                    pickerState.MethodFilter));
        }

        private static void AddSection(AdvancedDropdownItem root, AdvancedDropdownItem section)
        {
            if (section?.children.Count() > 0)
            {
                root.AddChild(section);
            }
        }

        private static AdvancedDropdownItem BuildSection(string sectionName, IEnumerable<FunctionRegistry.FunctionCandidate> candidates)
        {
            AdvancedDropdownItem section = new(sectionName);
            Dictionary<string, AdvancedDropdownItem> folders = new(StringComparer.Ordinal);
            Dictionary<AdvancedDropdownItem, List<FunctionRegistry.FunctionCandidate>> candidatesByFolder = new();

            foreach (FunctionRegistry.FunctionCandidate candidate in SortCandidates(candidates))
            {
                AdvancedDropdownItem parent = GetOrCreateFolder(section, folders, GetRelativePath(sectionName, candidate.Path));
                if (!candidatesByFolder.TryGetValue(parent, out List<FunctionRegistry.FunctionCandidate> folderCandidates))
                {
                    folderCandidates = new List<FunctionRegistry.FunctionCandidate>();
                    candidatesByFolder[parent] = folderCandidates;
                }

                folderCandidates.Add(candidate);
            }

            foreach (var item in candidatesByFolder)
            {
                AddCandidateItems(item.Key, item.Value);
            }

            return section;
        }

        private static IEnumerable<FunctionRegistry.FunctionCandidate> SortCandidates(IEnumerable<FunctionRegistry.FunctionCandidate> candidates)
        {
            return (candidates ?? Enumerable.Empty<FunctionRegistry.FunctionCandidate>())
                .Where(candidate => candidate?.Method != null)
                .OrderBy(candidate => candidate.Path)
                .ThenBy(candidate => candidate.SortKey)
                .ThenBy(candidate => candidate.DisplaySignature);
        }

        private static void AddCandidateItems(AdvancedDropdownItem parent, List<FunctionRegistry.FunctionCandidate> candidates)
        {
            foreach (IGrouping<string, FunctionRegistry.FunctionCandidate> group in candidates.GroupBy(GetOverloadGroupName, StringComparer.Ordinal))
            {
                if (group.Count() == 1)
                {
                    parent.AddChild(new FunctionPickerItem(group.First()));
                    continue;
                }

                AdvancedDropdownItem overloadGroup = new(group.Key);
                parent.AddChild(overloadGroup);
                foreach (FunctionRegistry.FunctionCandidate candidate in group)
                {
                    overloadGroup.AddChild(new FunctionPickerItem(candidate, candidate.GetDisplayParameterSignature()));
                }
            }
        }

        private static string GetOverloadGroupName(FunctionRegistry.FunctionCandidate candidate)
        {
            return candidate.GetDisplayCallableName();
        }

        private static AdvancedDropdownItem GetOrCreateFolder(
            AdvancedDropdownItem root,
            Dictionary<string, AdvancedDropdownItem> folders,
            string path)
        {
            AdvancedDropdownItem parent = root;
            string currentPath = string.Empty;

            foreach (string part in SplitPath(path))
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

        private static string GetRelativePath(string sectionName, string fullPath)
        {
            string[] parts = SplitPath(fullPath);
            if (parts.Length == 0)
            {
                return string.Empty;
            }

            int startIndex = string.Equals(parts[0], sectionName, StringComparison.Ordinal) ? 1 : 0;
            return string.Join("/", parts.Skip(startIndex));
        }

        private static string[] SplitPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return Array.Empty<string>();
            }

            return path
                .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries)
                .ToArray();
        }

        private sealed class FunctionPickerItem : AdvancedDropdownItem
        {
            public FunctionRegistry.FunctionCandidate Candidate { get; }

            public FunctionPickerItem(FunctionRegistry.FunctionCandidate candidate)
                : this(candidate, candidate.GetFullDisplayName())
            {
            }

            public FunctionPickerItem(FunctionRegistry.FunctionCandidate candidate, string displayName)
                : base(displayName)
            {
                Candidate = candidate;
            }
        }
    }
}
