using Amlos.AI.Nodes;
using Amlos.AI.References;
using System;
using System.Collections.Generic;

namespace Amlos.AI.Editor
{
    public interface INodeReferenceSlot
    {
        string Name { get; }
        bool Contains(TreeNode node);
        void Clear();
    }

    public interface INodeReferenceSingleSlot : INodeReferenceSlot
    {
        void Set(TreeNode treeNode);
    }

    public interface INodeReferenceListSlot : INodeReferenceSlot
    {
        int Count { get; }
        bool Add(TreeNode treeNode);
        void Insert(int index, TreeNode treeNode);
        int IndexOf(TreeNode treeNode);
        bool Remove(TreeNode treeNode);
    }

    public static class NodeReferenceSlotExtensions
    {
        public static List<INodeReferenceSlot> ToReferenceSlots(this TreeNode treeNode)
        {
            if (treeNode == null)
            {
                return new List<INodeReferenceSlot>();
            }

            var slots = new List<INodeReferenceSlot>();

            switch (treeNode)
            {
                case Condition:
                    slots.Add(Single(nameof(Condition.condition), static n => ((Condition)n).condition, static (n, v) => ((Condition)n).condition = v, treeNode));
                    slots.Add(Single(nameof(Condition.trueNode), static n => ((Condition)n).trueNode, static (n, v) => ((Condition)n).trueNode = v, treeNode));
                    slots.Add(Single(nameof(Condition.falseNode), static n => ((Condition)n).falseNode, static (n, v) => ((Condition)n).falseNode = v, treeNode));
                    break;

                case Sequence:
                    slots.Add(SequenceList(nameof(Sequence.events), treeNode));
                    break;

                case Decision:
                    slots.Add(DecisionList(nameof(Decision.events), treeNode));
                    break;

                case Probability:
                    slots.Add(ProbabilityEventWeightList(nameof(Probability.events), treeNode));
                    break;

                case PseudoProbability:
                    slots.Add(PseudoProbabilityEventWeightList(nameof(PseudoProbability.events), treeNode));
                    break;

                case Loop loop:
                    slots.Add(LoopList(nameof(Loop.events), treeNode));
                    slots.Add(Single(nameof(Loop.condition), static n => ((Loop)n).condition, static (n, v) => ((Loop)n).condition = v, treeNode));
                    break;

                case Inverter inverter:
                    slots.Add(Single(nameof(Inverter.node), static n => ((Inverter)n).node, static (n, v) => ((Inverter)n).node = v, treeNode));
                    break;

                case Update update:
                    slots.Add(Single(nameof(Update.subtreeHead), static n => ((Update)n).subtreeHead, static (n, v) => ((Update)n).subtreeHead = v, treeNode));
                    break;

                case Break b:
                    slots.Add(Single(nameof(Break.condition), static n => ((Break)n).condition, static (n, v) => ((Break)n).condition = v, treeNode));
                    break;

                case ForEach forEach:
                    slots.Add(Single(nameof(ForEach.@event), static n => ((ForEach)n).@event, static (n, v) => ((ForEach)n).@event = v, treeNode));
                    break;

                case WaitWhile waitWhile:
                    slots.Add(Single(nameof(WaitWhile.condition), static n => ((WaitWhile)n).condition, static (n, v) => ((WaitWhile)n).condition = v, treeNode));
                    break;

                case WaitUntil waitUntil:
                    slots.Add(Single(nameof(WaitUntil.condition), static n => ((WaitUntil)n).condition, static (n, v) => ((WaitUntil)n).condition = v, treeNode));
                    break;

                default:
                    break;
            }

            return slots;
        }

        public static INodeReferenceListSlot GetListSlot(this TreeNode treeNode)
        {
            return treeNode switch
            {
                Sequence => SequenceList(nameof(Sequence.events), treeNode),
                Decision => DecisionList(nameof(Decision.events), treeNode),
                Probability => ProbabilityEventWeightList(nameof(Probability.events), treeNode),
                PseudoProbability => PseudoProbabilityEventWeightList(nameof(PseudoProbability.events), treeNode),
                Loop => LoopList(nameof(Loop.events), treeNode),
                _ => null,
            };
        }

        public static bool DetachFrom(this TreeNode draggedNode, TreeNode oldParent)
        {
            if (oldParent == null)
            {
                return false;
            }

            var oldSlots = oldParent.ToReferenceSlots();
            for (int i = 0; i < oldSlots.Count; i++)
            {
                var slot = oldSlots[i];
                if (!slot.Contains(draggedNode))
                {
                    continue;
                }

                if (slot is INodeReferenceSingleSlot single)
                {
                    single.Clear();
                    return true;
                }

                if (slot is INodeReferenceListSlot list)
                {
                    list.Remove(draggedNode);
                    return true;
                }
            }
            return false;
        }




        private static INodeReferenceSingleSlot Single(string fieldName, Func<TreeNode, NodeReference> get, Action<TreeNode, NodeReference> set, TreeNode owner)
        {
            return new DelegateSingleSlot(ToTitleCase(fieldName), owner, get, set);
        }

        private static INodeReferenceListSlot LoopList(string fieldName, TreeNode owner)
        {
            return new NodeReferenceArraySlot(
                ToTitleCase(fieldName),
                owner,
                static n => ((Loop)n).events,
                static (n, v) => ((Loop)n).events = v);
        }

        private static INodeReferenceListSlot SequenceList(string fieldName, TreeNode owner)
        {
            return new NodeReferenceArraySlot(
                ToTitleCase(fieldName),
                owner,
                static n => ((Sequence)n).events,
                static (n, v) => ((Sequence)n).events = v);
        }

        private static INodeReferenceListSlot DecisionList(string fieldName, TreeNode owner)
        {
            return new NodeReferenceArraySlot(
                ToTitleCase(fieldName),
                owner,
                static n => ((Decision)n).events,
                static (n, v) => ((Decision)n).events = v);
        }

        private static INodeReferenceListSlot ProbabilityEventWeightList(string fieldName, TreeNode owner)
        {
            return new ProbabilityEventWeightArraySlot(
                ToTitleCase(fieldName),
                owner,
                static n => ((Probability)n).events,
                static (n, v) => ((Probability)n).events = v);
        }

        private static INodeReferenceListSlot PseudoProbabilityEventWeightList(string fieldName, TreeNode owner)
        {
            return new PseudoProbabilityEventWeightArraySlot(
                ToTitleCase(fieldName),
                owner,
                static n => ((PseudoProbability)n).events,
                static (n, v) => ((PseudoProbability)n).events = v);
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

        private sealed class DelegateSingleSlot : INodeReferenceSingleSlot
        {
            private readonly string name;
            private readonly TreeNode owner;
            private readonly Func<TreeNode, NodeReference> get;
            private readonly Action<TreeNode, NodeReference> set;

            public DelegateSingleSlot(
                string name,
                TreeNode owner,
                Func<TreeNode, NodeReference> get,
                Action<TreeNode, NodeReference> set)
            {
                this.name = name;
                this.owner = owner;
                this.get = get;
                this.set = set;
            }

            public string Name => name;

            public bool Contains(TreeNode node)
            {
                if (owner == null || node == null)
                {
                    return false;
                }

                NodeReference reference = get(owner);
                return reference != null && reference.UUID == node.UUID;
            }

            public void Clear()
            {
                if (owner == null)
                {
                    return;
                }

                set(owner, new NodeReference());
            }

            public void Set(TreeNode treeNode)
            {
                if (owner == null)
                {
                    return;
                }

                set(owner, treeNode == null ? new NodeReference() : new NodeReference(treeNode.UUID));
            }
        }

        private sealed class NodeReferenceArraySlot : INodeReferenceListSlot
        {
            private readonly string name;
            private readonly TreeNode owner;
            private readonly Func<TreeNode, NodeReference[]> get;
            private readonly Action<TreeNode, NodeReference[]> set;

            public NodeReferenceArraySlot(
                string name,
                TreeNode owner,
                Func<TreeNode, NodeReference[]> get,
                Action<TreeNode, NodeReference[]> set)
            {
                this.name = name;
                this.owner = owner;
                this.get = get;
                this.set = set;
            }

            public string Name => name;

            private NodeReference[] GetArray()
            {
                if (owner == null)
                {
                    return null;
                }

                return get(owner) ?? Array.Empty<NodeReference>();
            }

            private void SetArray(NodeReference[] arr)
            {
                if (owner == null)
                {
                    return;
                }

                set(owner, arr ?? Array.Empty<NodeReference>());
            }

            public int Count => GetArray().Length;

            public bool Contains(TreeNode node)
            {
                if (node == null)
                {
                    return false;
                }

                var arr = GetArray();
                for (int i = 0; i < arr.Length; i++)
                {
                    if (arr[i].UUID == node.UUID)
                    {
                        return true;
                    }
                }

                return false;
            }

            public void Clear()
            {
                SetArray(Array.Empty<NodeReference>());
            }

            public bool Add(TreeNode treeNode)
            {
                if (treeNode == null)
                {
                    return false;
                }

                var arr = GetArray();
                var newArr = new NodeReference[arr.Length + 1];
                Array.Copy(arr, newArr, arr.Length);
                newArr[^1] = new NodeReference(treeNode.UUID);
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

                var newArr = new NodeReference[arr.Length + 1];
                if (clampedIndex > 0)
                {
                    Array.Copy(arr, 0, newArr, 0, clampedIndex);
                }
                newArr[clampedIndex] = new NodeReference(treeNode.UUID);
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
                    if (arr[i].UUID == treeNode.UUID)
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
                    if (arr[i].UUID == treeNode.UUID)
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
                    SetArray(Array.Empty<NodeReference>());
                    return true;
                }

                var newArr = new NodeReference[arr.Length - 1];
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

        private sealed class ProbabilityEventWeightArraySlot : INodeReferenceListSlot
        {
            private readonly string name;
            private readonly TreeNode owner;
            private readonly Func<TreeNode, Probability.EventWeight[]> get;
            private readonly Action<TreeNode, Probability.EventWeight[]> set;

            public ProbabilityEventWeightArraySlot(
                string name,
                TreeNode owner,
                Func<TreeNode, Probability.EventWeight[]> get,
                Action<TreeNode, Probability.EventWeight[]> set)
            {
                this.name = name;
                this.owner = owner;
                this.get = get;
                this.set = set;
            }

            public string Name => name;

            private Probability.EventWeight[] GetArray()
            {
                if (owner == null)
                {
                    return null;
                }

                return get(owner) ?? Array.Empty<Probability.EventWeight>();
            }

            private void SetArray(Probability.EventWeight[] arr)
            {
                if (owner == null)
                {
                    return;
                }

                set(owner, arr ?? Array.Empty<Probability.EventWeight>());
            }

            public int Count => GetArray().Length;

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
            private readonly string name;
            private readonly TreeNode owner;
            private readonly Func<TreeNode, PseudoProbability.EventWeight[]> get;
            private readonly Action<TreeNode, PseudoProbability.EventWeight[]> set;

            public PseudoProbabilityEventWeightArraySlot(
                string name,
                TreeNode owner,
                Func<TreeNode, PseudoProbability.EventWeight[]> get,
                Action<TreeNode, PseudoProbability.EventWeight[]> set)
            {
                this.name = name;
                this.owner = owner;
                this.get = get;
                this.set = set;
            }

            public string Name => name;

            private PseudoProbability.EventWeight[] GetArray()
            {
                if (owner == null)
                {
                    return null;
                }

                return get(owner) ?? Array.Empty<PseudoProbability.EventWeight>();
            }

            private void SetArray(PseudoProbability.EventWeight[] arr)
            {
                if (owner == null)
                {
                    return;
                }

                set(owner, arr ?? Array.Empty<PseudoProbability.EventWeight>());
            }

            public int Count => GetArray().Length;

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