using Aethiumian.AI.Accessors;
using Aethiumian.AI.Editor;
using Aethiumian.AI.Nodes;
using Aethiumian.AI.References;
using Aethiumian.AI.Variables;
using Aethiumian.AI.Visual;
using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Aethiumian.AI.Editor.Tests.Graph
{
    /// <summary>
    /// EditMode coverage for graph topology and non-dirty layout resolution.
    /// </summary>
    public abstract class GraphEditorTestFixture
    {
        protected readonly List<BehaviourTreeData> trees = new();
        protected readonly List<AIEditorWindow> shownWindows = new();
        protected readonly List<AIEditorWindow> hiddenWindows = new();

        static GraphEditorTestFixture()
        {
            NodeDescriptorProvider.Register(new TestNodeDescriptor());
            NodeDescriptorProvider.Register(new TestHostDescriptor());
            NodeDescriptorProvider.Register(new TestServiceDescriptor());
            NodeReferenceStructureProvider.Register(typeof(TestNode), new TestNodeReferenceStructure());
            NodeReferenceStructureProvider.Register(typeof(TestHost), new TestHostReferenceStructure());
            NodeReferenceStructureProvider.Register(typeof(TestService), new TestServiceReferenceStructure());
        }

        [TearDown]
        public void TearDown()
        {
            AIEditorWindow.SharedClipboard.Clear();
            foreach (BehaviourTreeData tree in trees)
            {
                if (tree)
                {
                    UnityEngine.Object.DestroyImmediate(tree);
                }
            }

            trees.Clear();

            foreach (AIEditorWindow window in shownWindows)
            {
                if (window)
                {
                    window.Close();
                }
            }

            shownWindows.Clear();

            foreach (AIEditorWindow window in hiddenWindows)
            {
                if (window)
                {
                    UnityEngine.Object.DestroyImmediate(window);
                }
            }

            hiddenWindows.Clear();
        }

        protected BehaviourTreeData Tree(params TreeNode[] nodes)
        {
            BehaviourTreeData tree = ScriptableObject.CreateInstance<BehaviourTreeData>();
            tree.headNodeUUID = nodes[0].uuid;
            tree.nodes.AddRange(nodes);
            trees.Add(tree);
            return tree;
        }

        /// <summary>Creates a hidden graph module whose window is owned by this test fixture.</summary>
        private protected GraphEditorModule CreateHiddenGraphModule(BehaviourTreeData tree)
        {
            AIEditorWindow window = ScriptableObject.CreateInstance<AIEditorWindow>();
            hiddenWindows.Add(window);
            window.Load(tree);
            GraphEditorModule module = new(window);
            module.Attach(CreateDeclaredGraphHost(window));
            return module;
        }

        /// <summary>Creates and displays one Graph window for event-routing tests.</summary>
        protected AIEditorWindow ShowGraphWindow(BehaviourTreeData tree)
        {
            AIEditorWindow window = AIEditorWindow.ShowWindow(tree);
            shownWindows.Add(window);
            window.position = new Rect(100f, 100f, 1000f, 700f);
            window.CreateGUI();
            window.rootVisualElement.Q<ToolbarToggle>("ai-editor-graph-tab").value = true;
            return window;
        }

        /// <summary>Gets the Graph module owned by a displayed editor window.</summary>
        private protected static GraphEditorModule GetGraphModule(AIEditorWindow window)
        {
            return (GraphEditorModule)typeof(AIEditorWindow)
                .GetField("graphModule", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(window);
        }

        /// <summary>Reads a private UI field for event-routing assertions without changing production ownership.</summary>
        protected static T GetPrivateField<T>(object instance, string fieldName)
        {
            return (T)instance.GetType()
                .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(instance);
        }

        /// <summary>Dispatches one real UI Toolkit key event and reports whether Canvas consumed it.</summary>
        protected static bool SendKeyDown(VisualElement target, KeyCode keyCode, EventModifiers modifiers = EventModifiers.None)
        {
            Assert.That(target, Is.Not.Null);
            target.Focus();
            Assert.That(target.panel, Is.Not.Null);
            if (target.panel.focusController != null)
            {
                Assert.That(target.panel.focusController.focusedElement, Is.SameAs(target));
            }

            GraphCanvasElement canvas = target as GraphCanvasElement ?? target.GetFirstAncestorOfType<GraphCanvasElement>();
            if (canvas == null)
            {
                return false;
            }

            bool canvasStoppedEvent = false;
            EventCallback<KeyDownEvent> canvasCallback = evt => canvasStoppedEvent = evt.isPropagationStopped;
            canvas.RegisterCallback(canvasCallback);
            try
            {
                using KeyDownEvent evt = KeyDownEvent.GetPooled('\0', keyCode, modifiers);
                target.SendEvent(evt);
            }
            finally
            {
                canvas.UnregisterCallback(canvasCallback);
            }

            return canvasStoppedEvent;
        }

        /// <summary>Clones the editor's authoritative default-reference UXML and returns its Graph host.</summary>
        protected static VisualElement CreateDeclaredGraphHost(AIEditorWindow window)
        {
            SerializedObject serializedWindow = new(window);
            VisualTreeAsset shellAsset = serializedWindow.FindProperty("shellAsset").objectReferenceValue as VisualTreeAsset;
            Assert.That(shellAsset, Is.Not.Null);
            VisualElement root = new();
            shellAsset.CloneTree(root);
            return root.Q<VisualElement>("ai-editor-graph-host");
        }

        /// <summary>Sends a right- or left-button pointer-down event through the real UI Toolkit route.</summary>
        protected static void SendPointerDown(VisualElement target, int button, Vector2 position)
        {
            Event systemEvent = new()
            {
                type = EventType.MouseDown,
                button = button,
                mousePosition = position,
            };
            using PointerDownEvent pointerDown = PointerDownEvent.GetPooled(systemEvent);
            target.SendEvent(pointerDown);
        }

        /// <summary>Sends a pointer-down event and returns whether any route stopped propagation.</summary>
        protected static bool SendPointerDownAndGetPropagationState(VisualElement target, int button, Vector2 position)
        {
            Event systemEvent = new()
            {
                type = EventType.MouseDown,
                button = button,
                mousePosition = position,
            };
            using PointerDownEvent pointerDown = PointerDownEvent.GetPooled(systemEvent);
            target.SendEvent(pointerDown);
            return pointerDown.isPropagationStopped;
        }

        /// <summary>Sends a right- or left-button pointer-up event through the real UI Toolkit route.</summary>
        protected static void SendPointerUp(VisualElement target, int button, Vector2 position)
        {
            Event systemEvent = new()
            {
                type = EventType.MouseUp,
                button = button,
                mousePosition = position,
            };
            using PointerUpEvent pointerUp = PointerUpEvent.GetPooled(systemEvent);
            target.SendEvent(pointerUp);
        }

        /// <summary>Sends a complete primary-button click through the real UI Toolkit route.</summary>
        protected static void SendPointerClick(VisualElement target)
        {
            Vector2 position = target.worldBound.center;
            SendPointerDown(target, 0, position);
            SendPointerUp(target, 0, position);
        }

        /// <summary>Invokes a Button's internal clickable handler because UI Toolkit exposes no public API for simulating this callback.</summary>
        protected static void InvokeButtonClickable(Button target)
        {
            Assert.That(target, Is.Not.Null);
            const string InvokeMethodName = "Invoke";
            MethodInfo invokeMethod = typeof(Clickable).GetMethod(
                InvokeMethodName,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
                binder: null,
                types: new[] { typeof(EventBase) },
                modifiers: null);
            Assert.That(invokeMethod, Is.Not.Null);
            invokeMethod.Invoke(target.clickable, new object[] { null });
        }

        protected static T Node<T>(string name) where T : TreeNode, new()
        {
            return new T
            {
                name = name,
                uuid = UUID.NewUUID(),
            };
        }

        [Serializable]

        protected sealed class TestNode : TreeNode
        {
            public TestNode() { }

            public NodeReference child;
            public RawNodeReference raw;

            public override void Initialize() { }
            public override State Execute() => State.Success;
        }

        [Serializable]
        protected sealed class TestHost : ServiceHostNode
        {
            public TestHost() { }

            public NodeReference[] children = Array.Empty<NodeReference>();
            public RawNodeReference raw;

            public override void Initialize() { }
            public override State Execute() => State.Success;
        }

        [Serializable]
        protected sealed class TestService : Service
        {
            public TestService() { }

            public NodeReference child;

            public override bool IsReady => true;
            public override void UpdateTimer() { }
            public override void Initialize() { }
            public override State Execute() => State.Success;
        }

        private sealed class TestNodeDescriptor : NodeDescriptor<TestNode>
        {
            protected override void Copy(TestNode destination, TestNode source, DuplicateMode mode)
            {
                destination.name = source.name;
                destination.uuid = source.uuid;
                destination.parent = global::Aethiumian.AI.Accessors.Duplicate.Value(source.parent);
                destination.child = global::Aethiumian.AI.Accessors.Duplicate.Value(source.child);
                destination.raw = global::Aethiumian.AI.Accessors.Duplicate.Value(source.raw);
            }

            protected override void FillNull(TestNode node)
            {
                node.parent ??= NodeReference.Empty;
                node.child ??= NodeReference.Empty;
                node.raw ??= RawNodeReference.Empty;
            }

            protected override void VisitMembers(TestNode node, NodeMemberVisitor visitor)
            {
                visitor.VisitNodeReference(nameof(TreeNode.parent), node.parent);
                visitor.VisitNodeReference(nameof(TestNode.child), node.child);
                visitor.VisitNodeReference(nameof(TestNode.raw), node.raw);
            }
        }

        private sealed class TestHostDescriptor : NodeDescriptor<TestHost>
        {
            protected override void Copy(TestHost destination, TestHost source, DuplicateMode mode)
            {
                destination.name = source.name;
                destination.uuid = source.uuid;
                destination.parent = global::Aethiumian.AI.Accessors.Duplicate.Value(source.parent);
                destination.services = global::Aethiumian.AI.Accessors.Duplicate.List(source.services);
                destination.children = global::Aethiumian.AI.Accessors.Duplicate.Array(source.children);
                destination.raw = global::Aethiumian.AI.Accessors.Duplicate.Value(source.raw);
            }

            protected override void FillNull(TestHost node)
            {
                node.parent ??= NodeReference.Empty;
                node.services ??= new List<NodeReference>();
                node.children ??= Array.Empty<NodeReference>();
                node.raw ??= RawNodeReference.Empty;
            }

            protected override void VisitMembers(TestHost node, NodeMemberVisitor visitor)
            {
                visitor.VisitNodeReference(nameof(TreeNode.parent), node.parent);
                if (node.services != null)
                {
                    for (int index = 0; index < node.services.Count; index++)
                    {
                        visitor.VisitNodeReference($"{nameof(ServiceHostNode.services)}[{index}]", node.services[index]);
                    }
                }
                for (int index = 0; index < node.children.Length; index++)
                {
                    visitor.VisitNodeReference($"{nameof(TestHost.children)}[{index}]", node.children[index]);
                }
                visitor.VisitNodeReference(nameof(TestHost.raw), node.raw);
            }
        }

        private sealed class TestServiceDescriptor : NodeDescriptor<TestService>
        {
            protected override void Copy(TestService destination, TestService source, DuplicateMode mode)
            {
                destination.name = source.name;
                destination.uuid = source.uuid;
                destination.parent = global::Aethiumian.AI.Accessors.Duplicate.Value(source.parent);
                destination.services = global::Aethiumian.AI.Accessors.Duplicate.List(source.services);
                destination.child = global::Aethiumian.AI.Accessors.Duplicate.Value(source.child);
            }

            protected override void FillNull(TestService node)
            {
                node.parent ??= NodeReference.Empty;
                node.services ??= new List<NodeReference>();
                node.child ??= NodeReference.Empty;
            }

            protected override void VisitMembers(TestService node, NodeMemberVisitor visitor)
            {
                visitor.VisitNodeReference(nameof(TreeNode.parent), node.parent);
                if (node.services != null)
                {
                    for (int index = 0; index < node.services.Count; index++)
                    {
                        visitor.VisitNodeReference($"{nameof(ServiceHostNode.services)}[{index}]", node.services[index]);
                    }
                }
                visitor.VisitNodeReference(nameof(TestService.child), node.child);
            }
        }

        private sealed class TestNodeReferenceStructure : INodeReferenceStructure
        {
            public IReadOnlyList<INodeReferenceSlot> GetSlots(TreeNode owner)
            {
                TestNode node = (TestNode)owner;
                return new INodeReferenceSlot[]
                {
                    new DelegateNodeReferenceSingleSlot(node, nameof(TreeNode.parent), typeof(NodeReference), current => ((TestNode)current).parent, (current, value) => ((TestNode)current).parent = (NodeReference)value),
                    new DelegateNodeReferenceSingleSlot(node, nameof(TestNode.child), typeof(NodeReference), current => ((TestNode)current).child, (current, value) => ((TestNode)current).child = (NodeReference)value),
                    new DelegateNodeReferenceSingleSlot(node, nameof(TestNode.raw), typeof(RawNodeReference), current => ((TestNode)current).raw, (current, value) => ((TestNode)current).raw = (RawNodeReference)value),
                };
            }
        }

        private sealed class TestHostReferenceStructure : INodeReferenceStructure
        {
            public IReadOnlyList<INodeReferenceSlot> GetSlots(TreeNode owner)
            {
                TestHost node = (TestHost)owner;
                return new INodeReferenceSlot[]
                {
                    new DelegateNodeReferenceSingleSlot(node, nameof(TreeNode.parent), typeof(NodeReference), current => ((TestHost)current).parent, (current, value) => ((TestHost)current).parent = (NodeReference)value),
                    new ArrayNodeReferenceSlot<NodeReference>(nameof(TestHost.children), () => node.children, value => node.children = value),
                    new DelegateNodeReferenceSingleSlot(node, nameof(TestHost.raw), typeof(RawNodeReference), current => ((TestHost)current).raw, (current, value) => ((TestHost)current).raw = (RawNodeReference)value),
                };
            }
        }

        private sealed class TestServiceReferenceStructure : INodeReferenceStructure
        {
            public IReadOnlyList<INodeReferenceSlot> GetSlots(TreeNode owner)
            {
                TestService node = (TestService)owner;
                return new INodeReferenceSlot[]
                {
                    new DelegateNodeReferenceSingleSlot(node, nameof(TreeNode.parent), typeof(NodeReference), current => ((TestService)current).parent, (current, value) => ((TestService)current).parent = (NodeReference)value),
                    new DelegateNodeReferenceSingleSlot(node, nameof(TestService.child), typeof(NodeReference), current => ((TestService)current).child, (current, value) => ((TestService)current).child = (NodeReference)value),
                };
            }
        }

        public sealed class ArrayNodeReferenceSlot<T> : IIndexedNodeReferenceListSlot
            where T : class, INodeReference, new()
        {
            private readonly Func<T[]> getter;
            private readonly Action<T[]> setter;

            public ArrayNodeReferenceSlot(string name, Func<T[]> getter, Action<T[]> setter)
            {
                Name = name;
                this.getter = getter;
                this.setter = setter;
            }

            public string Name { get; }
            public int Count => getter()?.Length ?? 0;
            public INodeReference GetReference(int index) => getter() is T[] values && index >= 0 && index < values.Length ? values[index] : null;
            public bool Contains(TreeNode node) => IndexOf(node) >= 0;
            public void Clear() => setter(Array.Empty<T>());
            public bool Add(TreeNode node) { Insert(Count, node); return node != null; }
            public void Insert(int index, TreeNode node)
            {
                T[] source = getter() ?? Array.Empty<T>();
                int targetIndex = Math.Clamp(index, 0, source.Length);
                T[] result = new T[source.Length + 1];
                Array.Copy(source, 0, result, 0, targetIndex);
                result[targetIndex] = Create(node);
                Array.Copy(source, targetIndex, result, targetIndex + 1, source.Length - targetIndex);
                setter(result);
            }
            public void Set(int index, TreeNode node)
            {
                T[] values = getter();
                if (values == null || index < 0 || index >= values.Length) return;
                values[index] ??= new T();
                values[index].Set(node);
            }
            public void RemoveAt(int index)
            {
                T[] source = getter();
                if (source == null || index < 0 || index >= source.Length) return;
                T[] result = new T[source.Length - 1];
                if (index > 0) Array.Copy(source, 0, result, 0, index);
                if (index < source.Length - 1) Array.Copy(source, index + 1, result, index, source.Length - index - 1);
                setter(result);
            }
            public void Move(int sourceIndex, int destinationIndex)
            {
                T[] values = getter();
                if (values == null || sourceIndex < 0 || sourceIndex >= values.Length || destinationIndex < 0 || destinationIndex >= values.Length) return;
                T moved = values[sourceIndex];
                List<T> reordered = values.ToList();
                reordered.RemoveAt(sourceIndex);
                reordered.Insert(destinationIndex, moved);
                setter(reordered.ToArray());
            }
            public int IndexOf(TreeNode node)
            {
                if (node == null) return -1;
                T[] values = getter() ?? Array.Empty<T>();
                for (int index = 0; index < values.Length; index++)
                    if (values[index]?.UUID == node.uuid) return index;
                return -1;
            }
            public bool Remove(TreeNode node)
            {
                int index = IndexOf(node);
                if (index < 0) return false;
                RemoveAt(index);
                return true;
            }

            private static T Create(TreeNode node)
            {
                T reference = new();
                reference.Set(node);
                return reference;
            }
        }
    }
}
