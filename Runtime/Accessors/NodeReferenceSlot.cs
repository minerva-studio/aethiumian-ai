using Aethiumian.AI.Nodes;
using Aethiumian.AI.References;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Aethiumian.AI.Accessors;

namespace Aethiumian.AI.LegacyAccessors
{
    public interface INodeReferenceSlot
    {
        string Name { get; }
        bool Contains(TreeNode node);
        void Clear();
    }

    public interface INodeReferenceSingleSlot : INodeReferenceSlot
    {
        INodeReference GetReference();
        void Set(TreeNode treeNode);
    }

    public interface INodeReferenceListSlot : INodeReferenceSlot
    {
        int Count { get; }
        INodeReference GetReference(int index);
        bool Add(TreeNode treeNode);
        void Insert(int index, TreeNode treeNode);
        int IndexOf(TreeNode treeNode);
        bool Remove(TreeNode treeNode);
    }

    /// <summary>Provides a generated scalar reference slot without reflection.</summary>
    public sealed class DelegateNodeReferenceSingleSlot : INodeReferenceSingleSlot
    {
        private readonly TreeNode owner;
        private readonly Type fieldType;
        private readonly Func<TreeNode, INodeReference> getter;
        private readonly Action<TreeNode, INodeReference> setter;

        /// <summary>Initializes a delegate-backed scalar reference slot.</summary>
        public DelegateNodeReferenceSingleSlot(
            TreeNode owner,
            string name,
            Type fieldType,
            Func<TreeNode, INodeReference> getter,
            Action<TreeNode, INodeReference> setter)
        {
            this.owner = owner;
            Name = name;
            this.fieldType = fieldType;
            this.getter = getter;
            this.setter = setter;
        }

        public string Name { get; }

        public INodeReference GetReference() => owner == null ? null : getter(owner);

        public bool Contains(TreeNode node)
        {
            return node != null && GetReference()?.UUID == node.uuid;
        }

        public void Clear()
        {
            INodeReference reference = GetReference();
            if (reference != null)
            {
                reference.Clear();
                return;
            }

            setter(owner, CreateReference(null));
        }

        public void Set(TreeNode treeNode)
        {
            INodeReference reference = GetReference();
            if (reference == null)
            {
                reference = CreateReference(treeNode);
                setter(owner, reference);
                return;
            }

            reference.Set(treeNode);
        }

        private INodeReference CreateReference(TreeNode treeNode)
        {
            INodeReference reference = (INodeReference)Activator.CreateInstance(fieldType);
            reference.Set(treeNode);
            return reference;
        }
    }

    public static class NodeReferenceSlotExtensions
    {
        public static List<INodeReferenceSlot> ToReferenceSlots(this TreeNode treeNode)
        {
            if (treeNode == null)
            {
                return new List<INodeReferenceSlot>();
            }

            return NodeReferenceStructureProvider.GetSlots(treeNode)
                .Where(slot => slot.Name != nameof(treeNode.parent)
                    && slot.Name != nameof(ServiceHostNode.services))
                .ToList();
        }

        public static INodeReferenceListSlot GetListSlot(this TreeNode treeNode)
        {
            if (treeNode == null)
            {
                return null;
            }

            return NodeReferenceStructureProvider.GetListSlots(treeNode).FirstOrDefault();
        }

        private static INodeReferenceListSlot CreateListSlot(TreeNode owner, INodeReferenceCollectionFieldAccessor collectionAccessor)
        {
            if (collectionAccessor.CollectionType.IsArray)
            {
                if (collectionAccessor.ElementType == typeof(Probability.EventWeight))
                {
                    return new ProbabilityEventWeightArraySlot(owner, collectionAccessor);
                }

                if (collectionAccessor.ElementType == typeof(PseudoProbability.EventWeight))
                {
                    return new PseudoProbabilityEventWeightArraySlot(owner, collectionAccessor);
                }

                return new NodeReferenceArraySlot(owner, collectionAccessor);
            }

            return new NodeReferenceListSlot(owner, collectionAccessor);
        }

        private static string ToTitleCase(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return string.Empty;
            }

            if (name.Length == 1)
            {
                return char.ToUpperInvariant(name[0]).ToString();
            }

            return char.ToUpperInvariant(name[0]) + name.Substring(1);
        }

        private static INodeReference CreateReference(Type referenceType, TreeNode treeNode)
        {
            INodeReference reference = (INodeReference)Activator.CreateInstance(referenceType);
            reference.UUID = treeNode?.uuid ?? UUID.Empty;
            reference.Node = null;
            return reference;
        }

        private static IList CreateCollection(Type collectionType, Type elementType)
        {
            if (collectionType.IsInterface || collectionType.IsAbstract)
            {
                return (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType));
            }

            return (IList)Activator.CreateInstance(collectionType);
        }

        private sealed class AccessorSingleSlot : INodeReferenceSingleSlot
        {
            private readonly TreeNode owner;
            private readonly INodeReferenceFieldAccessor accessor;

            public AccessorSingleSlot(TreeNode owner, INodeReferenceFieldAccessor accessor)
            {
                this.owner = owner;
                this.accessor = accessor;
            }

            public string Name => accessor.Name;

            public INodeReference GetReference() => accessor.Get(owner);

            public bool Contains(TreeNode node)
            {
                if (owner == null || node == null)
                {
                    return false;
                }

                INodeReference reference = accessor.Get(owner);
                return reference != null && reference.UUID == node.UUID;
            }

            public void Clear()
            {
                if (owner == null)
                {
                    return;
                }

                accessor.Set(owner, CreateReference(accessor.FieldType, null));
            }

            public void Set(TreeNode treeNode)
            {
                if (owner == null)
                {
                    return;
                }

                accessor.Set(owner, CreateReference(accessor.FieldType, treeNode));
            }
        }

        private sealed class NodeReferenceArraySlot : INodeReferenceListSlot
        {
            private readonly TreeNode owner;
            private readonly INodeReferenceCollectionFieldAccessor accessor;

            public NodeReferenceArraySlot(TreeNode owner, INodeReferenceCollectionFieldAccessor accessor)
            {
                this.owner = owner;
                this.accessor = accessor;
            }

            public string Name => accessor.Name;

            public int Count => GetArray().Length;

            public INodeReference GetReference(int index)
            {
                Array array = GetArray();
                return index >= 0 && index < array.Length ? array.GetValue(index) as INodeReference : null;
            }

            private Array GetArray()
            {
                if (owner == null)
                {
                    return null;
                }

                return accessor.Get(owner) as Array ?? Array.CreateInstance(accessor.ElementType, 0);
            }

            private void SetArray(Array arr)
            {
                if (owner == null)
                {
                    return;
                }

                accessor.Set(owner, (IList)arr ?? Array.CreateInstance(accessor.ElementType, 0));
            }

            public bool Contains(TreeNode node)
            {
                if (node == null)
                {
                    return false;
                }

                var arr = GetArray();
                for (int i = 0; i < arr.Length; i++)
                {
                    if (arr.GetValue(i) is INodeReference reference && reference.UUID == node.UUID)
                    {
                        return true;
                    }
                }

                return false;
            }

            public void Clear()
            {
                SetArray(Array.CreateInstance(accessor.ElementType, 0));
            }

            public bool Add(TreeNode treeNode)
            {
                if (treeNode == null)
                {
                    return false;
                }

                var arr = GetArray();
                Array newArr = Array.CreateInstance(accessor.ElementType, arr.Length + 1);
                Array.Copy(arr, newArr, arr.Length);
                newArr.SetValue(CreateReference(accessor.ElementType, treeNode), newArr.Length - 1);
                SetArray(newArr);
                return true;
            }

            public void Insert(int index, TreeNode treeNode)
            {
                if (treeNode == null)
                {
                    return;
                }

                var arr = GetArray();
                int clampedIndex = index < 0 || index > arr.Length ? arr.Length : index;

                Array newArr = Array.CreateInstance(accessor.ElementType, arr.Length + 1);
                if (clampedIndex > 0)
                {
                    Array.Copy(arr, 0, newArr, 0, clampedIndex);
                }
                newArr.SetValue(CreateReference(accessor.ElementType, treeNode), clampedIndex);
                if (clampedIndex < arr.Length)
                {
                    Array.Copy(arr, clampedIndex, newArr, clampedIndex + 1, arr.Length - clampedIndex);
                }

                SetArray(newArr);
            }

            public int IndexOf(TreeNode treeNode)
            {
                if (treeNode == null)
                {
                    return -1;
                }

                var arr = GetArray();
                for (int i = 0; i < arr.Length; i++)
                {
                    if (arr.GetValue(i) is INodeReference reference && reference.UUID == treeNode.UUID)
                    {
                        return i;
                    }
                }

                return -1;
            }

            public bool Remove(TreeNode treeNode)
            {
                if (treeNode == null)
                {
                    return false;
                }

                var arr = GetArray();
                int idx = -1;
                for (int i = 0; i < arr.Length; i++)
                {
                    if (arr.GetValue(i) is INodeReference reference && reference.UUID == treeNode.UUID)
                    {
                        idx = i;
                        break;
                    }
                }

                if (idx < 0)
                {
                    return false;
                }

                if (arr.Length == 1)
                {
                    SetArray(Array.CreateInstance(accessor.ElementType, 0));
                    return true;
                }

                Array newArr = Array.CreateInstance(accessor.ElementType, arr.Length - 1);
                if (idx > 0)
                {
                    Array.Copy(arr, 0, newArr, 0, idx);
                }
                if (idx < arr.Length - 1)
                {
                    Array.Copy(arr, idx + 1, newArr, idx, arr.Length - idx - 1);
                }

                SetArray(newArr);
                return true;
            }
        }

        private sealed class NodeReferenceListSlot : INodeReferenceListSlot
        {
            private readonly TreeNode owner;
            private readonly INodeReferenceCollectionFieldAccessor accessor;

            public NodeReferenceListSlot(TreeNode owner, INodeReferenceCollectionFieldAccessor accessor)
            {
                this.owner = owner;
                this.accessor = accessor;
            }

            public string Name => accessor.Name;

            public int Count => GetList()?.Count ?? 0;

            public INodeReference GetReference(int index)
            {
                IList list = GetList();
                return list != null && index >= 0 && index < list.Count ? list[index] as INodeReference : null;
            }

            private IList GetList()
            {
                if (owner == null)
                {
                    return null;
                }

                return accessor.Get(owner);
            }

            private IList EnsureList()
            {
                if (owner == null)
                {
                    return null;
                }

                IList list = accessor.Get(owner);
                if (list == null)
                {
                    list = CreateCollection(accessor.CollectionType, accessor.ElementType);
                    accessor.Set(owner, list);
                }

                return list;
            }

            public bool Contains(TreeNode node)
            {
                if (node == null)
                {
                    return false;
                }

                IList list = GetList();
                if (list == null)
                {
                    return false;
                }

                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i] is INodeReference reference && reference.UUID == node.UUID)
                    {
                        return true;
                    }
                }

                return false;
            }

            public void Clear()
            {
                IList list = EnsureList();
                list?.Clear();
            }

            public bool Add(TreeNode treeNode)
            {
                if (treeNode == null)
                {
                    return false;
                }

                IList list = EnsureList();
                if (list == null)
                {
                    return false;
                }

                list.Add(CreateReference(accessor.ElementType, treeNode));
                return true;
            }

            public void Insert(int index, TreeNode treeNode)
            {
                if (treeNode == null)
                {
                    return;
                }

                IList list = EnsureList();
                if (list == null)
                {
                    return;
                }

                int clampedIndex = index < 0 || index > list.Count ? list.Count : index;
                list.Insert(clampedIndex, CreateReference(accessor.ElementType, treeNode));
            }

            public int IndexOf(TreeNode treeNode)
            {
                if (treeNode == null)
                {
                    return -1;
                }

                IList list = GetList();
                if (list == null)
                {
                    return -1;
                }

                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i] is INodeReference reference && reference.UUID == treeNode.UUID)
                    {
                        return i;
                    }
                }

                return -1;
            }

            public bool Remove(TreeNode treeNode)
            {
                if (treeNode == null)
                {
                    return false;
                }

                IList list = GetList();
                if (list == null)
                {
                    return false;
                }

                int index = -1;
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i] is INodeReference reference && reference.UUID == treeNode.UUID)
                    {
                        index = i;
                        break;
                    }
                }

                if (index < 0)
                {
                    return false;
                }

                list.RemoveAt(index);
                return true;
            }
        }

        private sealed class ProbabilityEventWeightArraySlot : INodeReferenceListSlot
        {
            private readonly TreeNode owner;
            private readonly INodeReferenceCollectionFieldAccessor accessor;

            public ProbabilityEventWeightArraySlot(TreeNode owner, INodeReferenceCollectionFieldAccessor accessor)
            {
                this.owner = owner;
                this.accessor = accessor;
            }

            public string Name => accessor.Name;

            public int Count => GetArray().Length;

            public INodeReference GetReference(int index)
            {
                Probability.EventWeight[] array = GetArray();
                return index >= 0 && index < array.Length ? array[index] : null;
            }

            private Probability.EventWeight[] GetArray()
            {
                if (owner == null)
                {
                    return null;
                }

                return accessor.Get(owner) as Probability.EventWeight[] ?? Array.Empty<Probability.EventWeight>();
            }

            private void SetArray(Probability.EventWeight[] arr)
            {
                if (owner == null)
                {
                    return;
                }

                accessor.Set(owner, arr ?? Array.Empty<Probability.EventWeight>());
            }

            public bool Contains(TreeNode node)
            {
                if (node == null)
                {
                    return false;
                }

                var arr = GetArray();
                for (int i = 0; i < arr.Length; i++)
                {
                    if (arr[i]?.reference.UUID == node.UUID)
                    {
                        return true;
                    }
                }

                return false;
            }

            public void Clear()
            {
                SetArray(Array.Empty<Probability.EventWeight>());
            }

            public bool Add(TreeNode treeNode)
            {
                if (treeNode == null)
                {
                    return false;
                }

                var arr = GetArray();
                var newArr = new Probability.EventWeight[arr.Length + 1];
                Array.Copy(arr, newArr, arr.Length);
                newArr[^1] = new Probability.EventWeight() { reference = new NodeReference(treeNode.UUID), weight = 1 };
                SetArray(newArr);
                return true;
            }

            public void Insert(int index, TreeNode treeNode)
            {
                if (treeNode == null)
                {
                    return;
                }

                var arr = GetArray();
                int clampedIndex = index < 0 || index > arr.Length ? arr.Length : index;

                int weight = 1;
                if (arr.Length > clampedIndex && clampedIndex > 0 && arr[clampedIndex] != null)
                {
                    weight = arr[clampedIndex].weight;
                }

                var newArr = new Probability.EventWeight[arr.Length + 1];
                if (clampedIndex > 0)
                {
                    Array.Copy(arr, 0, newArr, 0, clampedIndex);
                }
                newArr[clampedIndex] = new Probability.EventWeight() { reference = new NodeReference(treeNode.UUID), weight = weight };
                if (clampedIndex < arr.Length)
                {
                    Array.Copy(arr, clampedIndex, newArr, clampedIndex + 1, arr.Length - clampedIndex);
                }

                SetArray(newArr);
            }

            public int IndexOf(TreeNode treeNode)
            {
                if (treeNode == null)
                {
                    return -1;
                }

                var arr = GetArray();
                for (int i = 0; i < arr.Length; i++)
                {
                    if (arr[i]?.reference.UUID == treeNode.UUID)
                    {
                        return i;
                    }
                }

                return -1;
            }

            public bool Remove(TreeNode treeNode)
            {
                if (treeNode == null)
                {
                    return false;
                }

                var arr = GetArray();
                int idx = -1;
                for (int i = 0; i < arr.Length; i++)
                {
                    if (arr[i]?.reference.UUID == treeNode.UUID)
                    {
                        idx = i;
                        break;
                    }
                }

                if (idx < 0)
                {
                    return false;
                }

                if (arr.Length == 1)
                {
                    SetArray(Array.Empty<Probability.EventWeight>());
                    return true;
                }

                var newArr = new Probability.EventWeight[arr.Length - 1];
                if (idx > 0)
                {
                    Array.Copy(arr, 0, newArr, 0, idx);
                }
                if (idx < arr.Length - 1)
                {
                    Array.Copy(arr, idx + 1, newArr, idx, arr.Length - idx - 1);
                }

                SetArray(newArr);
                return true;
            }
        }

        private sealed class PseudoProbabilityEventWeightArraySlot : INodeReferenceListSlot
        {
            private readonly TreeNode owner;
            private readonly INodeReferenceCollectionFieldAccessor accessor;

            public PseudoProbabilityEventWeightArraySlot(TreeNode owner, INodeReferenceCollectionFieldAccessor accessor)
            {
                this.owner = owner;
                this.accessor = accessor;
            }

            public string Name => accessor.Name;

            public int Count => GetArray().Length;

            public INodeReference GetReference(int index)
            {
                PseudoProbability.EventWeight[] array = GetArray();
                return index >= 0 && index < array.Length ? array[index] : null;
            }

            private PseudoProbability.EventWeight[] GetArray()
            {
                if (owner == null)
                {
                    return null;
                }

                return accessor.Get(owner) as PseudoProbability.EventWeight[] ?? Array.Empty<PseudoProbability.EventWeight>();
            }

            private void SetArray(PseudoProbability.EventWeight[] arr)
            {
                if (owner == null)
                {
                    return;
                }

                accessor.Set(owner, arr ?? Array.Empty<PseudoProbability.EventWeight>());
            }

            public bool Contains(TreeNode node)
            {
                if (node == null)
                {
                    return false;
                }

                var arr = GetArray();
                for (int i = 0; i < arr.Length; i++)
                {
                    if (arr[i]?.reference.UUID == node.UUID)
                    {
                        return true;
                    }
                }

                return false;
            }

            public void Clear()
            {
                SetArray(Array.Empty<PseudoProbability.EventWeight>());
            }

            public bool Add(TreeNode treeNode)
            {
                if (treeNode == null)
                {
                    return false;
                }

                var arr = GetArray();
                var newArr = new PseudoProbability.EventWeight[arr.Length + 1];
                Array.Copy(arr, newArr, arr.Length);
                newArr[^1] = new PseudoProbability.EventWeight() { reference = new NodeReference(treeNode.UUID), weight = 1 };
                SetArray(newArr);
                return true;
            }

            public void Insert(int index, TreeNode treeNode)
            {
                if (treeNode == null)
                {
                    return;
                }

                var arr = GetArray();
                int clampedIndex = index < 0 || index > arr.Length ? arr.Length : index;

                int weight = 1;
                if (arr.Length > clampedIndex && clampedIndex > 0 && arr[clampedIndex] != null)
                {
                    weight = arr[clampedIndex].weight;
                }

                var newArr = new PseudoProbability.EventWeight[arr.Length + 1];
                if (clampedIndex > 0)
                {
                    Array.Copy(arr, 0, newArr, 0, clampedIndex);
                }
                newArr[clampedIndex] = new PseudoProbability.EventWeight() { reference = new NodeReference(treeNode.UUID), weight = weight };
                if (clampedIndex < arr.Length)
                {
                    Array.Copy(arr, clampedIndex, newArr, clampedIndex + 1, arr.Length - clampedIndex);
                }

                SetArray(newArr);
            }

            public int IndexOf(TreeNode treeNode)
            {
                if (treeNode == null)
                {
                    return -1;
                }

                var arr = GetArray();
                for (int i = 0; i < arr.Length; i++)
                {
                    if (arr[i]?.reference.UUID == treeNode.UUID)
                    {
                        return i;
                    }
                }

                return -1;
            }

            public bool Remove(TreeNode treeNode)
            {
                if (treeNode == null)
                {
                    return false;
                }

                var arr = GetArray();
                int idx = -1;
                for (int i = 0; i < arr.Length; i++)
                {
                    if (arr[i]?.reference.UUID == treeNode.UUID)
                    {
                        idx = i;
                        break;
                    }
                }

                if (idx < 0)
                {
                    return false;
                }

                if (arr.Length == 1)
                {
                    SetArray(Array.Empty<PseudoProbability.EventWeight>());
                    return true;
                }

                var newArr = new PseudoProbability.EventWeight[arr.Length - 1];
                if (idx > 0)
                {
                    Array.Copy(arr, 0, newArr, 0, idx);
                }
                if (idx < arr.Length - 1)
                {
                    Array.Copy(arr, idx + 1, newArr, idx, arr.Length - idx - 1);
                }

                SetArray(newArr);
                return true;
            }
        }
    }
}
