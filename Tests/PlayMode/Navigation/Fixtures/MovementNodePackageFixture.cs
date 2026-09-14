using System;
using System.Collections;
using System.Collections.Generic;
using Aethiumian.AI.Nodes;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Aethiumian.AI.Navigation.Tests
{
    /// <summary>
    /// Provides package-only AI and physics wiring for movement-node runtime tests.
    /// </summary>
    public abstract class MovementNodePackageFixture
    {
        protected const float BodyWidth = 0.8f;
        protected const float BodyHeight = 1.8f;
        protected const int RuntimeContractTickLimit = 120;
        protected static readonly Vector2 MovementStart = new(30.5f, 1f);

        private readonly List<GameObject> objects = new();
        private readonly List<BehaviourTreeData> trees = new();

        [UnityTearDown]
        public IEnumerator TearDownMovementFixture()
        {
            for (int index = objects.Count - 1; index >= 0; index--)
            {
                if (objects[index]) UnityEngine.Object.Destroy(objects[index]);
            }

            for (int index = trees.Count - 1; index >= 0; index--)
            {
                if (trees[index]) UnityEngine.Object.Destroy(trees[index]);
            }

            objects.Clear();
            trees.Clear();
            yield return null;
        }

        protected MovementHarness CreateHarness(Vector2 groundAnchor, TreeNode head, bool canMove = true)
        {
            GameObject host = new("package-movement-node");
            host.SetActive(false);
            objects.Add(host);
            host.transform.position = groundAnchor + Vector2.up * (BodyHeight * 0.5f);

            MovementTestSource source = host.AddComponent<MovementTestSource>();
            source.CanMove = canMove;
            Rigidbody2D body = host.AddComponent<Rigidbody2D>();
            body.gravityScale = 4f;
            body.linearDamping = 5f;
            body.freezeRotation = true;
            body.sleepMode = RigidbodySleepMode2D.NeverSleep;
            BoxCollider2D collider = host.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(BodyWidth, BodyHeight);

            BehaviourTreeData data = ScriptableObject.CreateInstance<BehaviourTreeData>();
            data.name = "Package_MovementNode";
            data.noActionMaximumDurationLimit = true;
            data.headNodeUUID = head.uuid;
            data.nodes.Add(head);
            trees.Add(data);

            AI ai = host.AddComponent<AI>();
            ai.ControlTarget = source;
            ai.awakeStart = true;
            ai.autoRestart = false;
            ai.Data = data;
            host.SetActive(true);
            Physics2D.SyncTransforms();
            return new MovementHarness(host, source, body, collider, ai);
        }

        protected GameObject CreateTraceTarget(Vector2 center)
        {
            GameObject target = new("package-movement-target");
            objects.Add(target);
            target.transform.position = center;
            BoxCollider2D collider = target.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(BodyWidth, BodyHeight);
            Physics2D.SyncTransforms();
            return target;
        }

        protected GameObject CreateGround(float y = 0f, float width = 120f)
        {
            GameObject ground = new("package-movement-ground")
            {
                layer = NavigationPhysicsTestLayers.GeometryLayer,
            };
            objects.Add(ground);
            ground.transform.position = new Vector2(40f, y - 0.25f);
            BoxCollider2D collider = ground.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(width, 0.5f);
            Physics2D.SyncTransforms();
            return ground;
        }

        protected static IEnumerator WaitForTreeCreated(MovementHarness harness)
        {
            for (int frame = 0; frame < RuntimeContractTickLimit; frame++)
            {
                if (harness.AI.BehaviourTree != null && harness.AI.BehaviourTree.IsInitialized)
                    yield break;
                yield return null;
            }

            Assert.That(harness.AI.BehaviourTree, Is.Not.Null);
            Assert.That(harness.AI.BehaviourTree.IsInitialized, Is.True);
            Assert.That(harness.AI.BehaviourTree.IsFaulted, Is.False);
        }

        protected static IEnumerator WaitForTerminal(MovementHarness harness, int limit)
        {
            for (int frame = 0; frame < limit && harness.AI.BehaviourTree.IsRunning; frame++)
                yield return new WaitForFixedUpdate();

            Assert.That(harness.AI.BehaviourTree.IsRunning, Is.False);
        }

        /// <summary>Waits until the test body has established physical contact with the fixture geometry.</summary>
        protected static IEnumerator WaitUntilGrounded(MovementHarness harness)
        {
            for (int frame = 0; frame < RuntimeContractTickLimit; frame++)
            {
                if (harness.Collider.IsTouchingLayers(NavigationPhysicsTestLayers.GeometryMask)) yield break;
                yield return new WaitForFixedUpdate();
            }

            Assert.That(harness.Collider.IsTouchingLayers(NavigationPhysicsTestLayers.GeometryMask), Is.True,
                DescribeHarness(harness));
        }

        /// <summary>Formats the package-owned runtime state when a movement assertion fails.</summary>
        protected static string DescribeHarness(MovementHarness harness)
            => $"Position={harness.Body.position}; Velocity={harness.Body.linearVelocity}; "
                + $"CanMove={harness.Source.CanMove}; TreeCreated={harness.AI.BehaviourTree != null}; "
                + $"Initialized={harness.AI.BehaviourTree?.IsInitialized}; "
                + $"Faulted={harness.AI.BehaviourTree?.IsFaulted}; "
                + $"Running={harness.AI.BehaviourTree?.IsRunning}; "
                + $"Result={harness.AI.BehaviourTree?.MainStack?.ReturnValue}.";

        protected sealed class RuntimeContextScope : IDisposable
        {
            private readonly MapNavigationRuntime previous = NavigationRuntimeContext.Current;

            internal RuntimeContextScope(MapNavigationRuntime runtime)
            {
                if (runtime == null) NavigationRuntimeContext.ClearCurrent(NavigationRuntimeContext.Current);
                else NavigationRuntimeContext.SetCurrent(runtime);
            }

            public void Dispose()
            {
                NavigationRuntimeContext.ClearCurrent(NavigationRuntimeContext.Current);
                if (previous != null && !previous.IsDisposed) NavigationRuntimeContext.SetCurrent(previous);
            }
        }

        protected sealed class MovementHarness
        {
            internal MovementHarness(GameObject host, MovementTestSource source, Rigidbody2D body,
                BoxCollider2D collider, AI ai)
            {
                Host = host;
                Source = source;
                Body = body;
                Collider = collider;
                AI = ai;
            }

            internal GameObject Host { get; }
            internal MovementTestSource Source { get; }
            internal Rigidbody2D Body { get; }
            internal BoxCollider2D Collider { get; }
            internal AI AI { get; }
        }

        protected sealed class MovementTestSource : MonoBehaviour, IMovementSource
        {
            public bool CanMove { get; set; } = true;

            public int WalkCount { get; private set; }
            public int JumpCount { get; private set; }

            public void OnWalk() => WalkCount++;

            public void OnJump() => JumpCount++;
        }
    }
}
