using Aethiumian.AI.Attributes;
using Aethiumian.AI.Nodes;
using Aethiumian.AI.References;
using Aethiumian.AI.Variables;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using AIAAnimator = Aethiumian.AI.Nodes.Animator;

namespace Aethiumian.AI.Editor.Exporting
{
    /// <summary>Exports loaded behaviour-tree assets as read-only semantic YAML.</summary>
    public static class BehaviourTreeDomExporter
    {
        /// <summary>
        /// Exports the tree head or a selected node without mutating the asset.
        /// </summary>
        /// <param name="tree">The loaded behaviour-tree asset.</param>
        /// <param name="startNode">The optional node UUID; empty selects the tree head.</param>
        /// <returns>A deterministic YAML document and any projection diagnostics.</returns>
        public static BehaviourTreeDomExportResult ExportYaml(BehaviourTreeData tree, UUID startNode = default)
        {
            if (tree == null)
            {
                throw new ArgumentNullException(nameof(tree));
            }

            DomExportContext context = new DomExportContext(tree, startNode);
            DomMapping document = context.BuildDocument();
            string content = context.StartNode == null
                ? string.Empty
                : DomYamlWriter.Write(document);
            return new BehaviourTreeDomExportResult(content, context.Diagnostics, context.ExportedNodeCount);
        }
    }

    internal sealed class DomExportContext
    {
        private readonly Dictionary<UUID, TreeNode> nodes;
        private readonly Dictionary<ReferenceKey, NodeReferenceOccurrence> occurrences = new Dictionary<ReferenceKey, NodeReferenceOccurrence>();
        private readonly HashSet<UUID> expanded = new HashSet<UUID>();
        private readonly HashSet<UUID> active = new HashSet<UUID>();
        private readonly List<BehaviourTreeDomDiagnostic> diagnostics = new List<BehaviourTreeDomDiagnostic>();
        private readonly Stack<string> path = new Stack<string>();
        private readonly NodeTopologySnapshot topology;
        private readonly DomProjectionMetadataCache metadata;
        private readonly IReadOnlyList<TreeNode> authoredNodes;
        private int variableReferenceCount;
        private int unresolvedReferenceCount;

        internal DomExportContext(BehaviourTreeData tree, UUID requestedStart)
        {
            Tree = tree;
            authoredNodes = tree.EditorNodes
                .Where(node => node != null)
                .ToArray();
            nodes = authoredNodes
                .GroupBy(node => node.uuid)
                .ToDictionary(group => group.Key, group => group.First());
            metadata = new DomProjectionMetadataCache(nodes.Values);
            topology = NodeTopologySnapshot.Create(tree.EditorNodes);

            foreach (TreeNode owner in nodes.Values)
            {
                foreach (NodeReferenceOccurrence occurrence in topology.GetOutgoing(owner))
                {
                    occurrences[new ReferenceKey(
                        owner.uuid,
                        occurrence.Address.FieldName,
                        occurrence.Address.Index)] = occurrence;
                }
            }

            UUID resolvedStart = requestedStart == UUID.Empty ? tree.headNodeUUID : requestedStart;
            if (!nodes.TryGetValue(resolvedStart, out TreeNode start))
            {
                AddDiagnostic("BTDOM_MISSING_START", BehaviourTreeDomDiagnosticSeverity.Error, resolvedStart, string.Empty,
                    resolvedStart == UUID.Empty ? "The behaviour tree has no head node." : $"The requested start node {resolvedStart} was not found.");
                return;
            }

            StartNode = start;
        }

        internal BehaviourTreeData Tree { get; }
        internal TreeNode StartNode { get; }
        internal int ExportedNodeCount => expanded.Count;
        internal IReadOnlyList<BehaviourTreeDomDiagnostic> Diagnostics => diagnostics;
        internal IReadOnlyList<TreeNode> AuthoredNodes => authoredNodes;
        internal int VariableReferenceCount => variableReferenceCount;
        internal int UnresolvedReferenceCount => unresolvedReferenceCount;

        /// <summary>Returns whether a node was included in the selected ownership projection.</summary>
        internal bool IsExported(UUID nodeId) => expanded.Contains(nodeId);

        /// <summary>Returns the cached semantic identity for a node type.</summary>
        internal DomTypeIdentity GetTypeIdentity(Type type) => metadata.GetTypeIdentity(type);

        internal DomMapping BuildDocument()
        {
            DomValue root = StartNode == null ? DomNull.Instance : ProjectNode(StartNode);
            int unreachableCount = nodes.Count - expanded.Count;
            if (unreachableCount > 0)
            {
                AddDiagnostic("BTDOM_UNREACHABLE_NODES", BehaviourTreeDomDiagnosticSeverity.Info, UUID.Empty, string.Empty,
                    $"{unreachableCount} authored node(s) are not reachable from the selected start node.");
            }

            DomMapping document = new DomMapping()
                .Add("schema", Scalar("aethiumian.behaviour-tree-dom/v1.1"))
                .Add("readonly", Scalar(true));

            DomMapping source = new DomMapping()
                .Add("assetPath", Scalar(AssetDatabase.GetAssetPath(Tree)))
                .Add("assetGuid", Scalar(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(Tree))))
                .Add("startNode", Scalar(StartNode?.uuid ?? UUID.Empty))
                .Add("totalNodeCount", Scalar(nodes.Count))
                .Add("exportedNodeCount", Scalar(expanded.Count))
                .Add("unreachableNodeCount", Scalar(unreachableCount));
            document.Add("source", source);
            document.Add("settings", ProjectTreeSettings());
            document.Add("context", ProjectTreeContext());
            document.Add("variables", ProjectVariables());
            document.Add("root", root);
            document.Add("diagnostics", ProjectDiagnostics());
            return document;
        }

        private DomMapping ProjectTreeSettings()
        {
            DomMapping settings = new DomMapping()
                .Add("actionMaximumDuration", Tree.noActionMaximumDurationLimit
                    ? Scalar("unlimited")
                    : Scalar(Tree.actionMaximumDuration))
                .Add("treeErrorHandle", Scalar(Tree.treeErrorHandle))
                .Add("nodeErrorHandle", Scalar(Tree.nodeErrorHandle));

            DomMapping randomSource = new DomMapping()
                .Add("scope", Scalar(Tree.randomSource.scope));
            if (Tree.randomSource.source != null)
            {
                randomSource.Add("source", ProjectUnityObject(Tree.randomSource.source));
            }

            settings.Add("randomSource", randomSource);
            return settings;
        }

        private DomMapping ProjectTreeContext()
        {
            DomMapping context = new DomMapping();
            if (Tree.targetScript != null)
            {
                context.Add("targetScript", ProjectUnityObject(Tree.targetScript));
            }

            if (Tree.prefab != null)
            {
                context.Add("prefab", ProjectUnityObject(Tree.prefab));
            }

            if (Tree.BaseAnimatorController != null)
            {
                context.Add("animatorController", ProjectUnityObject(Tree.BaseAnimatorController));
            }

            return context;
        }

        private DomSequence ProjectVariables()
        {
            DomSequence variables = new DomSequence();
            if (Tree.variables == null)
            {
                return variables;
            }

            foreach (VariableData variable in Tree.variables)
            {
                if (variable == null)
                {
                    continue;
                }

                DomMapping item = new DomMapping()
                    .Add("id", Scalar(variable.UUID))
                    .Add("name", Scalar(variable.name))
                    .Add("type", Scalar(variable.Type))
                    .Add("default", ProjectValue(variable.GetDefaultValue(), variable.ObjectType));
                if (variable.IsGlobal) item.Add("global", Scalar(true));
                if (variable.IsStatic) item.Add("static", Scalar(true));
                if (variable.IsStandardVariable) item.Add("standard", Scalar(true));
                if (!string.IsNullOrEmpty(variable.Path)) item.Add("path", Scalar(variable.Path));
                variables.Add(item);
            }

            return variables;
        }

        private DomSequence ProjectDiagnostics()
        {
            DomSequence result = new DomSequence();
            foreach (BehaviourTreeDomDiagnostic diagnostic in diagnostics)
            {
                result.Add(new DomMapping()
                    .Add("code", Scalar(diagnostic.Code))
                    .Add("severity", Scalar(diagnostic.Severity))
                    .Add("node", Scalar(diagnostic.NodeId))
                    .Add("field", Scalar(diagnostic.FieldPath))
                    .Add("occurrence", Scalar(diagnostic.SourceOccurrence))
                    .Add("message", Scalar(diagnostic.Message)));
            }

            return result;
        }

        private DomMapping ProjectNode(TreeNode node)
        {
            if (node == null)
            {
                return null;
            }

            if (active.Contains(node.uuid))
            {
                AddDiagnostic("BTDOM_CYCLE", BehaviourTreeDomDiagnosticSeverity.Error, node.uuid, CurrentPath,
                    "The authored ownership graph contains a cycle.");
                return ProjectReference(node);
            }

            if (!expanded.Add(node.uuid))
            {
                AddDiagnostic("BTDOM_MULTIPLE_OWNERS", BehaviourTreeDomDiagnosticSeverity.Error, node.uuid, CurrentPath,
                    "The node was reached by more than one owning occurrence.");
                return ProjectReference(node);
            }

            active.Add(node.uuid);
            DomTypeIdentity typeIdentity = metadata.GetTypeIdentity(node.GetType());
            DomMapping result = new DomMapping()
                .Add("id", Scalar(node.uuid))
                .Add("$type", Scalar(typeIdentity.ShortName));
            if (typeIdentity.IncludeClrType)
            {
                result.Add("clrType", Scalar(typeIdentity.FullName));
            }

            result.Add("name", Scalar(node.name))
                .Add("fields", ProjectNodeFields(node));
            active.Remove(node.uuid);
            return result;
        }

        private DomMapping ProjectNodeFields(TreeNode node)
        {
            DomMapping fields = new DomMapping();
            foreach (DomFieldMetadata fieldMetadata in metadata.GetFields(node.GetType()))
            {
                FieldInfo field = fieldMetadata.Field;
                if (field.Name == nameof(TreeNode.name) || field.Name == nameof(TreeNode.uuid) || field.Name == nameof(TreeNode.parent))
                {
                    continue;
                }

                if (fieldMetadata.IsIgnored && !fieldMetadata.IsService)
                {
                    continue;
                }

                if (!ConditionalFieldAttribute.IsTrue(node, field))
                {
                    continue;
                }

                if (!ShouldExportField(node, field))
                {
                    continue;
                }

                string fieldPath = AppendPath(field.Name);
                path.Push(field.Name);
                try
                {
                    object value = field.GetValue(node);
                    if (fieldMetadata.IsService && value is IList emptyServices && emptyServices.Count == 0)
                    {
                        continue;
                    }

                    if (field.Name == nameof(ObjectActionBase.parameters)
                        && node is ObjectActionBase objectAction)
                    {
                        fields.Add(field.Name, ProjectMethodParameters(objectAction, value as IList));
                    }
                    else if (field.Name == nameof(FunctionAction.parameters)
                        && node is FunctionAction functionAction)
                    {
                        fields.Add(field.Name, ProjectFunctionParameters(functionAction, value as IList));
                    }
                    else
                    {
                        fields.Add(field.Name, ProjectFieldValue(node, field, value));
                    }
                }
                catch (Exception exception)
                {
                    AddDiagnostic("BTDOM_FIELD_ERROR", BehaviourTreeDomDiagnosticSeverity.Warning, node.uuid, fieldPath,
                        $"Unable to project field: {exception.Message}");
                    fields.Add(field.Name, DomNull.Instance);
                }
                finally
                {
                    path.Pop();
                }
            }

            return fields;
        }

        private bool ShouldExportField(TreeNode node, FieldInfo field)
        {
            if (node is ObjectActionBase objectAction)
            {
                if (objectAction.actionCallTime == ObjectActionBase.ActionCallTime.once
                    && (field.Name == nameof(ObjectActionBase.actionCallTime)
                        || field.Name == nameof(ObjectActionBase.endType)
                        || field.Name == nameof(ObjectActionBase.duration)
                        || field.Name == nameof(ObjectActionBase.count)))
                {
                    return false;
                }

                if (field.Name == nameof(ObjectActionBase.duration)
                    && objectAction.endType != ObjectActionBase.UpdateEndType.byTimer)
                {
                    return false;
                }

                if (field.Name == nameof(ObjectActionBase.count)
                    && objectAction.endType != ObjectActionBase.UpdateEndType.byCounter)
                {
                    return false;
                }
            }

            return true;
        }

        private DomValue ProjectFieldValue(TreeNode owner, FieldInfo field, object value)
        {
            if (field.Name == nameof(ServiceHostNode.services) && value is IList services)
            {
                return ProjectReferenceCollection(owner, field.Name, services);
            }

            if (owner is AIAAnimator && field.Name == nameof(AIAAnimator.parameters) && value is IList animatorParameters)
            {
                return ProjectAnimatorParameters(animatorParameters);
            }

            if (value is IList list)
            {
                return ProjectReferenceCollectionOrValues(owner, field.Name, list);
            }

            if (value is INodeReference reference)
            {
                return ProjectReferenceValue(owner, field.Name, -1, reference);
            }

            return ProjectValue(value, field.FieldType);
        }

        private DomValue ProjectReferenceCollectionOrValues(TreeNode owner, string fieldName, IList list)
        {
            DomSequence result = new DomSequence();
            for (int index = 0; index < list.Count; index++)
            {
                object item = list[index];
                if (item is INodeReference reference)
                {
                    result.Add(ProjectConnectionEntry(owner, fieldName, index, item, reference));
                }
                else
                {
                    result.Add(ProjectValue(item, item?.GetType()));
                }
            }

            return result;
        }

        private DomValue ProjectReferenceCollection(TreeNode owner, string fieldName, IList list)
        {
            return ProjectReferenceCollectionOrValues(owner, fieldName, list);
        }

        private DomValue ProjectConnectionEntry(TreeNode owner, string fieldName, int index, object item, INodeReference reference)
        {
            if (item is NodeReference || item is RawNodeReference || HasOnlyReferenceFields(item.GetType()))
            {
                return ProjectReferenceValue(owner, fieldName, index, reference);
            }

            return ProjectObject(item, new ReferenceKey(owner.uuid, fieldName, index));
        }

        private DomValue ProjectReferenceValue(TreeNode owner, string fieldName, int index, INodeReference reference)
        {
            if (reference == null || reference.UUID == UUID.Empty)
            {
                return DomNull.Instance;
            }

            if (reference.IsRawReference)
            {
                return ProjectReferenceTarget(reference.UUID, true);
            }

            if (occurrences.TryGetValue(new ReferenceKey(owner.uuid, fieldName, index), out NodeReferenceOccurrence occurrence))
            {
                return ProjectNode(occurrence.Target);
            }

            if (nodes.TryGetValue(reference.UUID, out TreeNode target))
            {
                return ProjectNode(target);
            }

            unresolvedReferenceCount++;
            AddDiagnostic("BTDOM_MISSING_NODE", BehaviourTreeDomDiagnosticSeverity.Warning, owner.uuid, CurrentPath,
                $"Node reference {reference.UUID} could not be resolved.",
                FormatOccurrence(owner.uuid, fieldName, index));
            return ProjectReferenceTarget(reference.UUID, false);
        }

        private DomMapping ProjectObject(object value, ReferenceKey connection)
        {
            DomMapping result = new DomMapping();
            foreach (DomFieldMetadata fieldMetadata in metadata.GetFields(value.GetType()))
            {
                if (fieldMetadata.IsIgnored)
                {
                    continue;
                }

                FieldInfo field = fieldMetadata.Field;
                if (!ConditionalFieldAttribute.IsTrue(value, field))
                {
                    continue;
                }

                object fieldValue = field.GetValue(value);
                path.Push(field.Name);
                try
                {
                    if (fieldValue is INodeReference nestedReference
                        && nestedReference.UUID != UUID.Empty
                        && nestedReference.UUID == GetConnectionTarget(connection))
                    {
                        if (occurrences.TryGetValue(connection, out NodeReferenceOccurrence occurrence))
                        {
                            result.Add(field.Name, ProjectNode(occurrence.Target));
                        }
                        else
                        {
                            result.Add(field.Name, ProjectReferenceTarget(nestedReference.UUID, nestedReference.IsRawReference));
                        }
                    }
                    else
                    {
                        result.Add(field.Name, ProjectValue(fieldValue, field.FieldType));
                    }
                }
                finally
                {
                    path.Pop();
                }
            }

            return result;
        }

        private UUID GetConnectionTarget(ReferenceKey connection)
        {
            return occurrences.TryGetValue(connection, out NodeReferenceOccurrence occurrence)
                ? occurrence.Target.uuid
                : UUID.Empty;
        }

        private DomValue ProjectValue(object value, Type declaredType)
        {
            if (value == null)
            {
                return DomNull.Instance;
            }

            if (value is VariableFieldBase variableField)
            {
                return ProjectVariable(variableField);
            }

            if (value is AIAAnimator.Parameter animatorParameter)
            {
                return ProjectAnimatorParameter(animatorParameter);
            }

            if (value is INodeReference reference)
            {
                return reference.UUID == UUID.Empty ? DomNull.Instance : ProjectReferenceTarget(reference.UUID, reference.IsRawReference);
            }

            if (value is TreeNode node)
            {
                return ProjectNode(node);
            }

            if (value is UnityEngine.Object unityObject)
            {
                return ProjectUnityObject(unityObject);
            }

            if (value is TypeReference typeReference)
            {
                return ProjectTypeReference(typeReference);
            }

            if (value is UUID uuid)
            {
                return Scalar(uuid);
            }

            if (value is string || value is char || value is bool || value is Enum
                || value is byte || value is sbyte || value is short || value is ushort
                || value is int || value is uint || value is long || value is ulong
                || value is float || value is double || value is decimal)
            {
                return Scalar(value);
            }

            if (value is IList list)
            {
                DomSequence result = new DomSequence();
                foreach (object item in list)
                {
                    result.Add(ProjectValue(item, item?.GetType()));
                }

                return result;
            }

            return ProjectObject(value, default);
        }

        private DomSequence ProjectAnimatorParameters(IList parameters)
        {
            DomSequence result = new DomSequence();
            foreach (object item in parameters)
            {
                if (item is AIAAnimator.Parameter parameter && parameter.use)
                {
                    result.Add(ProjectAnimatorParameter(parameter));
                }
            }

            return result;
        }

        private DomMapping ProjectAnimatorParameter(AIAAnimator.Parameter parameter)
        {
            DomMapping result = new DomMapping()
                .Add("parameter", Scalar(parameter.parameter))
                .Add("type", Scalar(parameter.type));
            switch (parameter.type)
            {
                case AIAAnimator.ParameterType.@int:
                    result.Add("value", ProjectVariable(parameter.valueInt));
                    break;
                case AIAAnimator.ParameterType.@float:
                    result.Add("value", ProjectVariable(parameter.valueFloat));
                    break;
                case AIAAnimator.ParameterType.@bool:
                    result.Add("value", ProjectVariable(parameter.valueBool));
                    break;
                case AIAAnimator.ParameterType.trigger:
                    result.Add("trigger", Scalar(parameter.setTrigger));
                    break;
            }

            return result;
        }

        private DomValue ProjectVariable(VariableFieldBase variable)
        {
            if (variable.HasEditorReference && variable.UUID != UUID.Empty)
            {
                variableReferenceCount++;
                VariableData data = Tree.GetVariable(variable.UUID);
                return new DomMapping()
                    .Add("$var", Scalar(data?.name ?? VariableData.MISSING_VARIABLE_NAME))
                    .Add("id", Scalar(variable.UUID));
            }

            if (variable is not VariableValueFieldBase valueField)
            {
                return DomNull.Instance;
            }

            object constant = valueField.Value;
            if (constant == null)
            {
                return DomNull.Instance;
            }

            return ProjectValue(constant, variable.FieldObjectType);
        }

        private DomMapping ProjectReferenceTarget(UUID uuid, bool raw)
        {
            DomMapping result = new DomMapping().Add("$ref", Scalar(uuid));
            if (nodes.TryGetValue(uuid, out TreeNode target))
            {
                DomTypeIdentity typeIdentity = metadata.GetTypeIdentity(target.GetType());
                result.Add("name", Scalar(target.name))
                    .Add("$type", Scalar(typeIdentity.ShortName));
                if (typeIdentity.IncludeClrType)
                {
                    result.Add("clrType", Scalar(typeIdentity.FullName));
                }
            }

            if (raw)
            {
                result.Add("raw", Scalar(true));
            }

            return result;
        }

        private DomMapping ProjectReference(TreeNode node)
        {
            return ProjectReferenceTarget(node.uuid, false);
        }

        private DomMapping ProjectUnityObject(UnityEngine.Object value)
        {
            if (value == null)
            {
                return null;
            }

            string assetPath = AssetDatabase.GetAssetPath(value);
            string guid = string.IsNullOrEmpty(assetPath) ? string.Empty : AssetDatabase.AssetPathToGUID(assetPath);
            long localId = 0;
            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(value, out string resolvedGuid, out localId))
            {
                resolvedGuid = guid;
            }

            DomTypeIdentity typeIdentity = metadata.GetTypeIdentity(value.GetType());
            DomMapping result = new DomMapping()
                .Add("type", Scalar(typeIdentity.ShortName))
                .Add("name", Scalar(value.name));
            if (typeIdentity.IncludeClrType)
            {
                result.Add("clrType", Scalar(typeIdentity.FullName));
            }
            if (!string.IsNullOrEmpty(assetPath)) result.Add("path", Scalar(assetPath));
            if (!string.IsNullOrEmpty(resolvedGuid)) result.Add("guid", Scalar(resolvedGuid));
            if (localId != 0) result.Add("localId", Scalar(localId));
            if (string.IsNullOrEmpty(assetPath))
            {
                AddDiagnostic("BTDOM_NONPERSISTENT_OBJECT", BehaviourTreeDomDiagnosticSeverity.Warning, UUID.Empty, CurrentPath,
                    $"Unity object {value.name} is not a persistent asset.");
            }

            return result;
        }

        /// <summary>Projects a serialized type reference using the shared type identity rules.</summary>
        private DomMapping ProjectTypeReference(TypeReference typeReference)
        {
            DomTypeIdentity typeIdentity = metadata.GetTypeIdentity(typeReference.fullName, typeReference.assemblyName);
            DomMapping result = new DomMapping()
                .Add("type", Scalar(typeIdentity.ShortName))
                .Add("assembly", Scalar(typeReference.assemblyName));
            if (typeIdentity.IncludeClrType)
            {
                result.Add("clrType", Scalar(typeIdentity.FullName));
            }

            return result;
        }

        private DomSequence ProjectMethodParameters(ObjectActionBase action, IList parameters)
        {
            MethodInfo method = ResolveObjectActionMethod(action);
            if (method == null && !string.IsNullOrEmpty(action.methodName))
            {
                AddDiagnostic("BTDOM_UNRESOLVED_METHOD", BehaviourTreeDomDiagnosticSeverity.Warning, action.uuid, CurrentPath,
                    $"Unable to resolve object action method {action.methodName}.");
            }

            return ProjectParameterList(action, parameters, method);
        }

        private DomSequence ProjectFunctionParameters(FunctionAction action, IList parameters)
        {
            MethodInfo method = FunctionRegistry.Resolve(action.function);
            if (method == null && action.function != null && action.function.HasMethod)
            {
                AddDiagnostic("BTDOM_UNRESOLVED_METHOD", BehaviourTreeDomDiagnosticSeverity.Warning, action.uuid, CurrentPath,
                    $"Unable to resolve function {action.function.methodName}.");
            }

            return ProjectParameterList(action, parameters, method);
        }

        private DomSequence ProjectParameterList(TreeNode owner, IList parameters, MethodInfo method)
        {
            DomSequence result = new DomSequence();
            if (parameters == null)
            {
                return result;
            }

            ParameterInfo[] methodParameters = method?.GetParameters() ?? Array.Empty<ParameterInfo>();
            for (int index = 0; index < parameters.Count; index++)
            {
                object item = parameters[index];
                if (item is not Parameter parameter)
                {
                    result.Add(ProjectValue(item, item?.GetType()));
                    continue;
                }

                ParameterInfo info = index < methodParameters.Length ? methodParameters[index] : null;
                DomMapping entry = new DomMapping();
                if (info != null)
                {
                    DomTypeIdentity parameterType = metadata.GetTypeIdentity(info.ParameterType);
                    entry.Add("name", Scalar(info.Name))
                        .Add("type", Scalar(parameterType.ShortName));
                    if (parameterType.IncludeClrType)
                    {
                        entry.Add("clrType", Scalar(parameterType.FullName));
                    }
                    if (info.ParameterType == typeof(NodeProgress) || info.ParameterType == typeof(System.Threading.CancellationToken))
                    {
                        entry.Add("source", Scalar("injected"));
                        result.Add(entry);
                        continue;
                    }
                }
                else
                {
                    entry.Add("name", Scalar("arg" + index));
                    if (parameter.FieldObjectType != null)
                    {
                        DomTypeIdentity parameterType = metadata.GetTypeIdentity(parameter.FieldObjectType);
                        entry.Add("type", Scalar(parameterType.ShortName));
                        if (parameterType.IncludeClrType)
                        {
                            entry.Add("clrType", Scalar(parameterType.FullName));
                        }
                    }
                }

                try
                {
                    entry.Add("value", ProjectVariable(parameter));
                }
                catch (Exception exception)
                {
                    AddDiagnostic("BTDOM_PARAMETER_ERROR", BehaviourTreeDomDiagnosticSeverity.Warning, owner.uuid, CurrentPath,
                        $"Unable to project parameter {index}: {exception.Message}");
                    entry.Add("value", DomNull.Instance);
                }

                result.Add(entry);
            }

            return result;
        }

        private static MethodInfo ResolveObjectActionMethod(ObjectActionBase action)
        {
            if (action is not ObjectAction objectAction || objectAction.type?.ReferType == null || string.IsNullOrEmpty(action.methodName))
            {
                return null;
            }

            return objectAction.type.ReferType
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy)
                .FirstOrDefault(method => method.Name == action.methodName
                    && MethodCallers.ParameterMatches(method, objectAction.parameters ?? new List<Parameter>()));
        }

        private bool HasOnlyReferenceFields(Type type)
        {
            IReadOnlyList<DomFieldMetadata> fields = metadata.GetFields(type);
            return fields.Count == 0 || fields.All(field => typeof(INodeReference).IsAssignableFrom(field.Field.FieldType));
        }

        private DomScalar Scalar(object value)
        {
            if (value is UUID uuid)
            {
                return new DomScalar(uuid.ToString());
            }

            return new DomScalar(value);
        }

        private string AppendPath(string name)
        {
            return string.IsNullOrEmpty(CurrentPath) ? name : CurrentPath + "." + name;
        }

        private string CurrentPath => path.Count == 0 ? string.Empty : string.Join(".", path.Reverse());

        private void AddDiagnostic(
            string code,
            BehaviourTreeDomDiagnosticSeverity severity,
            UUID nodeId,
            string fieldPath,
            string message,
            string sourceOccurrence = null)
        {
            diagnostics.Add(new BehaviourTreeDomDiagnostic(code, severity, nodeId, fieldPath, message, sourceOccurrence));
        }

        private static string FormatOccurrence(UUID owner, string fieldName, int index)
        {
            if (index < 0)
            {
                return owner + "." + fieldName;
            }

            return owner + "." + fieldName + "[" + index + "]";
        }

        private readonly struct ReferenceKey : IEquatable<ReferenceKey>
        {
            internal ReferenceKey(UUID owner, string fieldName, int index)
            {
                Owner = owner;
                FieldName = fieldName ?? string.Empty;
                Index = index;
            }

            internal UUID Owner { get; }
            internal string FieldName { get; }
            internal int Index { get; }

            public bool Equals(ReferenceKey other)
            {
                return Owner == other.Owner && Index == other.Index && string.Equals(FieldName, other.FieldName, StringComparison.Ordinal);
            }

            public override bool Equals(object obj) => obj is ReferenceKey other && Equals(other);
            public override int GetHashCode() => HashCode.Combine(Owner, FieldName, Index);
        }
    }
}
