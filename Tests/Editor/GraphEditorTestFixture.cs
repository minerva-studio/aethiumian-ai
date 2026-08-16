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

namespace Aethiumian.AI.Tests
{
    /// <summary>
    /// EditMode coverage for graph topology and non-dirty layout resolution.
    /// </summary>
    public abstract class GraphEditorTestFixture
    {
        protected readonly List<BehaviourTreeData> trees = new();
        protected readonly List<AIEditorWindow> shownWindows = new();
        protected readonly List<AIEditorWindow> hiddenWindows = new();

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
            public NodeReference child;
            public RawNodeReference raw;

            public override void Initialize() { }
            public override State Execute() => State.Success;
        }

        [Serializable]
        protected sealed class TestHost : ServiceHostNode
        {
            public NodeReference[] children = Array.Empty<NodeReference>();
            public RawNodeReference raw;

            public override void Initialize() { }
            public override State Execute() => State.Success;
        }

        [Serializable]
        protected sealed class TestService : Service
        {
            public NodeReference child;

            public override bool IsReady => true;
            public override void UpdateTimer() { }
            public override void Initialize() { }
            public override State Execute() => State.Success;
        }
    }
}
