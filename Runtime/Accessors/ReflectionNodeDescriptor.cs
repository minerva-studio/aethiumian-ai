#if UNITY_EDITOR
#nullable enable
using Aethiumian.AI.Nodes;
using Aethiumian.AI.References;
using Aethiumian.AI.Variables;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Aethiumian.AI.Accessors
{
    /// <summary>
    /// Supplies the editor-only descriptor used for internal editor and test nodes that do not need generated code.
    /// </summary>
    internal sealed class ReflectionNodeDescriptor : NodeDescriptor
    {
        private static readonly BindingFlags InstanceFieldFlags =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly;

        private readonly Type nodeType;
        private readonly FieldPlan[] fields;

        private ReflectionNodeDescriptor(Type nodeType)
        {
            this.nodeType = nodeType;
            fields = BuildFieldPlans(nodeType);
            ReferenceStructure = new ReflectionNodeReferenceStructure(fields);
        }

        /// <summary>Gets the editable direct-reference structure for this node type.</summary>
        internal INodeReferenceStructure ReferenceStructure { get; }

        /// <summary>Determines whether a type can be handled by the editor fallback.</summary>
        internal static bool IsEligible(Type type)
        {
            return type != null &&
                typeof(TreeNode).IsAssignableFrom(type) &&
                !type.IsAbstract &&
                !type.IsGenericTypeDefinition &&
                type.GetConstructor(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    binder: null,
                    types: Type.EmptyTypes,
                    modifiers: null) != null;
        }

        /// <summary>Creates a reflection descriptor after validating the node field contract.</summary>
        internal static ReflectionNodeDescriptor Create(Type type)
        {
            if (!IsEligible(type))
            {
                throw new InvalidOperationException(
                    $"Node type '{type?.FullName}' is not eligible for the editor reflection descriptor.");
            }

            return new ReflectionNodeDescriptor(type);
        }

        /// <inheritdoc />
        public override Type NodeType => nodeType;

        /// <inheritdoc />
        internal override TreeNode CreateInstance()
        {
            return (TreeNode)Activator.CreateInstance(
                nodeType,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null,
                args: null,
                culture: null)!;
        }

        /// <inheritdoc />
        internal override TreeNode Duplicate(TreeNode source, DuplicateMode mode)
        {
            TreeNode destination = CreateInstance();
            Copy(destination, source, mode);
            return destination;
        }

        /// <inheritdoc />
        public override void Copy(TreeNode destination, TreeNode source, DuplicateMode mode)
        {
            ValidateNode(destination, nameof(destination));
            ValidateNode(source, nameof(source));

            foreach (FieldPlan field in fields)
            {
                field.Copy(destination, source, mode);
            }
        }

        /// <inheritdoc />
        public override void FillNull(TreeNode node)
        {
            ValidateNode(node, nameof(node));

            foreach (FieldPlan field in fields)
            {
                field.FillNull(node);
            }
        }

        /// <inheritdoc />
        public override void VisitMembers(TreeNode node, NodeMemberVisitor visitor)
        {
            ValidateNode(node, nameof(node));
            if (visitor == null)
            {
                throw new ArgumentNullException(nameof(visitor));
            }

            foreach (FieldPlan field in fields)
            {
                field.VisitMembers(node, visitor);
            }
        }

        private void ValidateNode(TreeNode node, string parameterName)
        {
            if (node == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            if (node.GetType() != nodeType)
            {
                throw new ArgumentException(
                    $"Expected node type '{nodeType.FullName}', received '{node.GetType().FullName}'.",
                    parameterName);
            }
        }

        private static FieldPlan[] BuildFieldPlans(Type type)
        {
            List<Type> hierarchy = new();
            for (Type? current = type; current != null; current = current.BaseType)
            {
                hierarchy.Add(current);
            }

            hierarchy.Reverse();
            List<FieldPlan> result = new();
            foreach (Type current in hierarchy)
            {
                foreach (FieldInfo field in current.GetFields(InstanceFieldFlags))
                {
                    if (ShouldInspectField(field))
                    {
                        result.Add(FieldPlan.Create(field));
                    }
                }
            }

            return result.ToArray();
        }

        private static bool ShouldInspectField(FieldInfo field)
        {
            if (field.IsStatic || field.IsLiteral || field.IsInitOnly || field.IsNotSerialized)
            {
                return false;
            }

            switch (field.Name)
            {
                case "behaviourTree":
                case "callStack":
                case "serviceHead":
                case "IsRunning":
                case "Prototype":
                    return false;
                default:
                    return true;
            }
        }

        internal enum FieldKind
        {
            Immutable,
            UnityObject,
            Duplicable,
            NodeMember,
            NodeReference,
            VariableBinding,
        }

        internal enum CollectionKind
        {
            None,
            Array,
            List,
        }

        internal sealed class FieldPlan
        {
            private FieldPlan(
                FieldInfo field,
                FieldKind kind,
                CollectionKind collectionKind,
                Type? elementType,
                bool runtimeShared,
                bool exposesNodeReference,
                bool exposesVariable,
                bool exposesMember)
            {
                Field = field;
                Kind = kind;
                CollectionKind = collectionKind;
                ElementType = elementType;
                RuntimeShared = runtimeShared;
                ExposesNodeReference = exposesNodeReference;
                ExposesVariable = exposesVariable;
                ExposesMember = exposesMember;
            }

            internal FieldInfo Field { get; }
            internal FieldKind Kind { get; }
            internal CollectionKind CollectionKind { get; }
            internal Type? ElementType { get; }
            internal bool RuntimeShared { get; }
            internal bool ExposesNodeReference { get; }
            internal bool ExposesVariable { get; }
            internal bool ExposesMember { get; }
            internal bool IsCollection => CollectionKind != CollectionKind.None;

            internal static FieldPlan Create(FieldInfo field)
            {
                bool runtimeShared = field.IsDefined(typeof(global::Aethiumian.AI.RuntimeSharedAttribute), false);
                if (TryGetCollectionElement(field.FieldType, out Type? elementType, out CollectionKind collectionKind))
                {
                    FieldKind elementKind = Classify(elementType!);
                    bool exposesNodeReference = typeof(INodeReference).IsAssignableFrom(elementType!);
                    bool exposesVariable = typeof(IVariableBinding).IsAssignableFrom(elementType!);
                    bool exposesMember = typeof(INodeMember).IsAssignableFrom(elementType!);
                    if (runtimeShared && (exposesNodeReference || exposesVariable))
                    {
                        throw Unsupported(field, "RuntimeShared cannot be used with runtime-bound collection elements.");
                    }

                    return new FieldPlan(
                        field,
                        elementKind,
                        collectionKind,
                        elementType,
                        runtimeShared,
                        exposesNodeReference,
                        exposesVariable,
                        exposesMember);
                }

                FieldKind kind = Classify(field.FieldType);
                bool fieldExposesNodeReference = typeof(INodeReference).IsAssignableFrom(field.FieldType);
                bool fieldExposesVariable = typeof(IVariableBinding).IsAssignableFrom(field.FieldType);
                bool fieldExposesMember = typeof(INodeMember).IsAssignableFrom(field.FieldType);
                if (runtimeShared && (fieldExposesNodeReference || fieldExposesVariable))
                {
                    throw Unsupported(field, "RuntimeShared cannot be used with runtime-bound fields.");
                }

                return new FieldPlan(
                    field,
                    kind,
                    CollectionKind.None,
                    elementType: null,
                    runtimeShared,
                    fieldExposesNodeReference,
                    fieldExposesVariable,
                    fieldExposesMember);
            }

            internal void Copy(TreeNode destination, TreeNode source, DuplicateMode mode)
            {
                object? sourceValue = Field.GetValue(source);
                object? destinationValue = RuntimeShared && mode == DuplicateMode.Instantiate
                    ? sourceValue
                    : DuplicateValue(sourceValue, Field.FieldType, CollectionKind, ElementType);
                Field.SetValue(destination, destinationValue);
            }

            internal void FillNull(TreeNode node)
            {
                if (Field.GetValue(node) != null)
                {
                    return;
                }

                object? value = null;
                if (Field.FieldType == typeof(string))
                {
                    value = string.Empty;
                }
                else if (Field.FieldType == typeof(NodeReference))
                {
                    value = NodeReference.Empty;
                }
                else if (Field.FieldType == typeof(RawNodeReference))
                {
                    value = RawNodeReference.Empty;
                }
                else if (CollectionKind == CollectionKind.Array)
                {
                    value = Array.CreateInstance(ElementType!, 0);
                }
                else if (CollectionKind == CollectionKind.List)
                {
                    value = Activator.CreateInstance(Field.FieldType);
                }

                if (value != null)
                {
                    Field.SetValue(node, value);
                }
            }

            internal void VisitMembers(TreeNode node, NodeMemberVisitor visitor)
            {
                object? value = Field.GetValue(node);
                if (IsCollection)
                {
                    if (value is not IList collection)
                    {
                        return;
                    }

                    for (int index = 0; index < collection.Count; index++)
                    {
                        VisitSingle(visitor, collection[index], Field.Name + "[" + index + "]");
                    }

                    return;
                }

                VisitSingle(visitor, value, Field.Name);
            }

            private void VisitSingle(NodeMemberVisitor visitor, object? value, string path)
            {
                if (ExposesMember)
                {
                    visitor.VisitMember(path, value as INodeMember);
                }
                else
                {
                    if (ExposesNodeReference)
                    {
                        visitor.VisitNodeReference(path, value as INodeReference);
                    }

                    if (ExposesVariable)
                    {
                        visitor.VisitVariableBinding(path, value as IVariableBinding);
                    }
                }
            }

            private static FieldKind Classify(Type type)
            {
                if (typeof(IDuplicable).IsAssignableFrom(type))
                {
                    return FieldKind.Duplicable;
                }

                if (typeof(INodeMember).IsAssignableFrom(type))
                {
                    return FieldKind.NodeMember;
                }

                if (type.IsValueType || type == typeof(string) || type == typeof(Type))
                {
                    return FieldKind.Immutable;
                }

                if (typeof(UnityEngine.Object).IsAssignableFrom(type))
                {
                    return FieldKind.UnityObject;
                }

                if (typeof(INodeReference).IsAssignableFrom(type))
                {
                    return FieldKind.NodeReference;
                }

                if (typeof(IVariableBinding).IsAssignableFrom(type))
                {
                    return FieldKind.VariableBinding;
                }

                throw Unsupported(null, $"Field type '{type.FullName}' is not supported by the reflection descriptor.");
            }

            private static bool TryGetCollectionElement(
                Type type,
                out Type? elementType,
                out CollectionKind collectionKind)
            {
                if (type.IsArray)
                {
                    if (type.GetArrayRank() != 1)
                    {
                        throw Unsupported(null, $"Collection type '{type.FullName}' must be a one-dimensional array.");
                    }

                    elementType = type.GetElementType();
                    collectionKind = CollectionKind.Array;
                    return true;
                }

                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
                {
                    elementType = type.GetGenericArguments()[0];
                    collectionKind = CollectionKind.List;
                    return true;
                }

                elementType = null;
                collectionKind = CollectionKind.None;
                return false;
            }

            private static object? DuplicateValue(
                object? value,
                Type declaredType,
                CollectionKind collectionKind,
                Type? elementType)
            {
                if (value == null)
                {
                    return null;
                }

                if (collectionKind == CollectionKind.Array)
                {
                    Array source = (Array)value;
                    Array result = Array.CreateInstance(elementType!, source.Length);
                    for (int index = 0; index < source.Length; index++)
                    {
                        result.SetValue(DuplicateElement(source.GetValue(index), elementType!), index);
                    }

                    return result;
                }

                if (collectionKind == CollectionKind.List)
                {
                    IList source = (IList)value;
                    IList result = (IList)(Activator.CreateInstance(declaredType)
                        ?? throw new InvalidOperationException($"Collection type '{declaredType.FullName}' has no parameterless constructor."));
                    foreach (object? item in source)
                    {
                        result.Add(DuplicateElement(item, elementType!));
                    }

                    return result;
                }

                return DuplicateElement(value, declaredType);
            }

            private static object? DuplicateElement(object? value, Type declaredType)
            {
                if (value == null)
                {
                    return null;
                }

                if (value is IDuplicable duplicable)
                {
                    return duplicable.Duplicate();
                }

                if (declaredType.IsValueType || value is string || value is Type || value is UnityEngine.Object)
                {
                    return value;
                }

                throw new InvalidOperationException(
                    $"Type '{value.GetType().FullName}' does not support duplicate. " +
                    "Mutable reference types must implement IDuplicable.");
            }

            private static InvalidOperationException Unsupported(FieldInfo? field, string reason)
            {
                string prefix = field == null
                    ? string.Empty
                    : $"Field '{field.DeclaringType?.FullName}.{field.Name}' is unsupported. ";
                return new InvalidOperationException(prefix + reason);
            }
        }

        private sealed class ReflectionNodeReferenceStructure : INodeReferenceStructure
        {
            private readonly IReadOnlyList<FieldPlan> fields;

            public ReflectionNodeReferenceStructure(IReadOnlyList<FieldPlan> fields)
            {
                this.fields = fields;
            }

            public IReadOnlyList<INodeReferenceSlot> GetSlots(TreeNode owner)
            {
                List<INodeReferenceSlot> slots = new();
                foreach (FieldPlan field in fields)
                {
                    if (!field.ExposesNodeReference)
                    {
                        continue;
                    }

                    if (field.IsCollection)
                    {
                        slots.Add(new ReflectionNodeReferenceListSlot(field, owner));
                    }
                    else
                    {
                        slots.Add(new DelegateNodeReferenceSingleSlot(
                            owner,
                            field.Field.Name,
                            field.Field.FieldType,
                            instance => field.Field.GetValue(instance) as INodeReference,
                            (instance, value) => field.Field.SetValue(instance, value)));
                    }
                }

                return slots;
            }
        }

        private sealed class ReflectionNodeReferenceListSlot : IIndexedNodeReferenceListSlot
        {
            private readonly FieldPlan field;
            private readonly TreeNode owner;

            public ReflectionNodeReferenceListSlot(FieldPlan field, TreeNode owner)
            {
                this.field = field;
                this.owner = owner;
            }

            public string Name => field.Field.Name;

            public int Count => GetCollection()?.Count ?? 0;

            public INodeReference GetReference(int index)
            {
                IList? collection = GetCollection();
                return collection != null && index >= 0 && index < collection.Count
                    ? collection[index] as INodeReference
                    : null;
            }

            public bool Contains(TreeNode node) => IndexOf(node) >= 0;

            public void Clear()
            {
                IList? collection = GetCollection();
                if (field.CollectionKind == CollectionKind.Array || collection == null)
                {
                    SetCollection(CreateCollection(0));
                }
                else
                {
                    collection.Clear();
                }
            }

            public bool Add(TreeNode treeNode)
            {
                if (treeNode == null)
                {
                    return false;
                }

                Insert(Count, treeNode);
                return true;
            }

            public void Insert(int index, TreeNode treeNode)
            {
                IList source = GetCollection() ?? CreateCollection(0);
                int targetIndex = Math.Max(0, Math.Min(index, source.Count));
                IList result = CreateCollection(source.Count + 1);
                for (int current = 0; current < targetIndex; current++) result[current] = source[current];
                result[targetIndex] = CreateElement(treeNode);
                for (int current = targetIndex; current < source.Count; current++) result[current + 1] = source[current];
                SetCollection(result);
            }

            public void Set(int index, TreeNode treeNode)
            {
                IList? collection = GetCollection();
                if (collection == null || index < 0 || index >= collection.Count)
                {
                    return;
                }

                if (collection[index] is INodeReference reference)
                {
                    reference.Set(treeNode);
                }
                else
                {
                    collection[index] = CreateElement(treeNode);
                }
            }

            public void RemoveAt(int index)
            {
                IList? source = GetCollection();
                if (source == null || index < 0 || index >= source.Count)
                {
                    return;
                }

                IList result = CreateCollection(source.Count - 1);
                for (int current = 0, target = 0; current < source.Count; current++)
                {
                    if (current != index) result[target++] = source[current];
                }

                SetCollection(result);
            }

            public bool Remove(TreeNode treeNode)
            {
                int index = IndexOf(treeNode);
                if (index < 0)
                {
                    return false;
                }

                RemoveAt(index);
                return true;
            }

            public int IndexOf(TreeNode treeNode)
            {
                if (treeNode == null)
                {
                    return -1;
                }

                IList? collection = GetCollection();
                if (collection == null)
                {
                    return -1;
                }

                for (int index = 0; index < collection.Count; index++)
                {
                    if (collection[index] is INodeReference reference && reference.UUID == treeNode.uuid)
                    {
                        return index;
                    }
                }

                return -1;
            }

            public void Move(int sourceIndex, int destinationIndex)
            {
                IList? source = GetCollection();
                if (source == null || sourceIndex < 0 || sourceIndex >= source.Count
                    || destinationIndex < 0 || destinationIndex >= source.Count
                    || sourceIndex == destinationIndex)
                {
                    return;
                }

                List<object?> values = source.Cast<object?>().ToList();
                object? moved = values[sourceIndex];
                values.RemoveAt(sourceIndex);
                values.Insert(destinationIndex, moved);
                IList result = CreateCollection(values.Count);
                for (int index = 0; index < values.Count; index++) result[index] = values[index];
                SetCollection(result);
            }

            private IList? GetCollection()
            {
                return field.Field.GetValue(GetOwner()) as IList;
            }

            private TreeNode GetOwner()
            {
                return owner;
            }

            private void SetCollection(IList value)
            {
                field.Field.SetValue(owner, value);
            }

            private IList CreateCollection(int count)
            {
                if (field.CollectionKind == CollectionKind.Array)
                {
                    return Array.CreateInstance(field.ElementType!, count);
                }

                IList result = (IList)(Activator.CreateInstance(field.Field.FieldType)
                    ?? throw new InvalidOperationException($"Collection type '{field.Field.FieldType.FullName}' cannot be created."));
                for (int index = 0; index < count; index++)
                {
                    result.Add(null);
                }

                return result;
            }

            private object CreateElement(TreeNode treeNode)
            {
                object element = Activator.CreateInstance(
                    field.ElementType!,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    binder: null,
                    args: null,
                    culture: null)!;
                if (element is not INodeReference reference)
                {
                    throw new InvalidOperationException($"Collection element '{field.ElementType!.FullName}' is not an INodeReference.");
                }

                reference.Set(treeNode);
                return element;
            }
        }
    }
}
#endif
