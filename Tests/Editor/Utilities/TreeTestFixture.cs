#nullable enable
using Aethiumian.AI.Nodes;
using Aethiumian.AI.References;
using Aethiumian.AI.Variables;
using NUnit.Framework;
using System;
using System.Collections;
using UnityEngine;

namespace Aethiumian.AI.Tests
{
    /// <summary>
    /// Reusable test fixture that creates a <see cref="BehaviourTree"/> in Edit Mode, provides manual and coroutine-based frame simulation, and cleans up on dispose.
    /// </summary>
    /// <remarks>
    /// Because these tests run in Edit Mode (<c>includePlatforms: ["Editor"]</c>), MonoBehaviour callbacks such as <c>Update</c> and <c>FixedUpdate</c> are never invoked automatically.
    /// This fixture exposes <see cref="Tick"/> to advance one full simulation frame (<c>Update → LateUpdate → FixedUpdate</c>) and coroutine helpers (<see cref="TickForFrames"/>, <see cref="WaitUntil"/>) for multi-frame sequences.
    /// </remarks>
    public sealed class TreeTestFixture : IDisposable
    {
        private readonly BehaviourTreeData data;
        private readonly GameObject gameObject;

        /// <summary>
        /// The constructed <see cref="BehaviourTree"/> ready for testing.
        /// </summary>
        public BehaviourTree Tree { get; }

        /// <summary>
        /// The <see cref="BehaviourTreeData"/> asset that backs the tree.
        /// </summary>
        public BehaviourTreeData Data => data;

        /// <summary>
        /// The host <see cref="GameObject"/> that owns the tree and its control script.
        /// </summary>
        public GameObject GameObject => gameObject;

        private TreeTestFixture(BehaviourTreeData data, GameObject gameObject, BehaviourTree tree)
        {
            this.data = data;
            this.gameObject = gameObject;
            Tree = tree;
        }

        // ── Static factories ──────────────────────────────────────────────

        /// <summary>
        /// Create a fixture with the given head node and optional additional nodes.
        /// </summary>
        public static TreeTestFixture Create(TreeNode head, params TreeNode[] nodes)
        {
            return Create(head, Array.Empty<VariableData>(), nodes);
        }

        /// <summary>
        /// Create a fixture with the given head node, variables, and optional nodes.
        /// </summary>
        public static TreeTestFixture Create(TreeNode head, VariableData[] variables, params TreeNode[] nodes)
        {
            return Create(head, variables, NodeErrorSolution.False, nodes);
        }

        /// <summary>
        /// Create a fixture with full configuration.
        /// </summary>
        /// <param name="head">The root node of the tree.</param>
        /// <param name="variables">Variable data to register on the tree.</param>
        /// <param name="nodeErrorSolution">Default error handling strategy.</param>
        /// <param name="nodes">Additional nodes to include in the tree data.</param>
        public static TreeTestFixture Create(
            TreeNode head,
            VariableData[] variables,
            NodeErrorSolution nodeErrorSolution,
            params TreeNode[] nodes)
        {
            BehaviourTreeData data = ScriptableObject.CreateInstance<BehaviourTreeData>();
            data.noActionMaximumDurationLimit = true;
            data.nodeErrorHandle = nodeErrorSolution;
            data.headNodeUUID = head.uuid;
            data.variables.AddRange(variables);
            data.nodes.Add(head);

            foreach (TreeNode node in nodes)
            {
                if (node.uuid != head.uuid)
                {
                    data.nodes.Add(node);
                }
            }

            GameObject gameObject = new("TreeTestFixture");
            TestBehaviour script = gameObject.AddComponent<TestBehaviour>();
            BehaviourTree tree = new(data, gameObject, script);
            return new TreeTestFixture(data, gameObject, tree);
        }

        // ── Node factory ──────────────────────────────────────────────────

        /// <summary>
        /// Create a named tree node with a fresh UUID for testing.
        /// </summary>
        public static T CreateNode<T>(string name) where T : TreeNode, new()
        {
            return new T
            {
                name = name,
                uuid = UUID.NewUUID(),
                parent = NodeReference.Empty,
            };
        }

        // ── Frame simulation (Edit Mode safe) ─────────────────────────────

        /// <summary>
        /// Advance the simulation by one full frame:
        /// <c>Update() → LateUpdate() → FixedUpdate()</c>.
        /// </summary>
        /// <remarks>
        /// This mirrors the order that the runtime <see cref="AI"/> MonoBehaviour
        /// calls on the tree every frame.  <see cref="BehaviourTree.FixedUpdate"/>
        /// internally drives <c>ServiceUpdate</c>, so service timers and intervals
        /// advance on every tick.
        /// </remarks>
        public void Tick()
        {
            if (!Tree.IsRunning) return;
            Tree.Update();
            Tree.LateUpdate();
            Tree.FixedUpdate();
        }

        /// <summary>
        /// Coroutine that advances the simulation by <paramref name="count"/> frames,
        /// yielding after each frame.
        /// </summary>
        public IEnumerator TickForFrames(int count)
        {
            for (int i = 0; i < count; i++)
            {
                Tick();
                yield return null;
            }
        }

        /// <summary>
        /// Coroutine that ticks the simulation each frame until
        /// <paramref name="condition"/> returns <c>true</c> or the timeout expires.
        /// </summary>
        /// <param name="condition">The predicate to evaluate after each tick.</param>
        /// <param name="timeoutSeconds">
        /// Maximum real-time to wait before giving up (default 5 s).
        /// </param>
        public IEnumerator WaitUntil(Func<bool> condition, float timeoutSeconds = 5f)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline)
            {
                Tick();
                yield return null;
            }
        }

        /// <summary>
        /// Coroutine that waits until the tree has finished asynchronous
        /// initialization (or an error occurs).
        /// </summary>
        public IEnumerator WaitUntilReady(float timeoutSeconds = 5f)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!Tree.IsInitialized && !Tree.IsError && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(Tree.IsError, Is.False, "Tree encountered an error during initialization.");
            Assert.That(Tree.IsInitialized, Is.True, "Tree did not finish initializing within the timeout.");
        }

        // ── Runtime node access ───────────────────────────────────────────

        /// <summary>
        /// Resolve the runtime instance of a prototype node.
        /// </summary>
        /// <typeparam name="T">The expected runtime type, inferred from the prototype.</typeparam>
        /// <param name="prototype">The prototype node whose UUID is looked up.</param>
        public T GetRuntimeNode<T>(T prototype) where T : TreeNode
        {
            return (T)Tree.References[prototype.uuid]!;
        }

        // ── Lifecycle ─────────────────────────────────────────────────────

        /// <summary>
        /// Start the behaviour tree.  Must be called after <see cref="WaitUntilReady"/>.
        /// </summary>
        public void Start()
        {
            Tree.Start();
        }

        /// <summary>
        /// Clean up the tree, host GameObject, and data asset.
        /// </summary>
        public void Dispose()
        {
            if (Tree.IsRunning) Tree.End();
            UnityEngine.Object.DestroyImmediate(gameObject);
            UnityEngine.Object.DestroyImmediate(data);
        }

        // ── Internal helpers ──────────────────────────────────────────────

        /// <summary>
        /// Minimal <see cref="MonoBehaviour"/> used as the control target
        /// required by the <see cref="BehaviourTree"/> constructor.
        /// </summary>
        private sealed class TestBehaviour : MonoBehaviour
        {
        }
    }
}
