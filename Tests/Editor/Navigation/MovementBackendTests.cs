using Aethiumian.AI.Navigation;
using Aethiumian.AI.Nodes;
using Aethiumian.AI.References;
using NUnit.Framework;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.TestTools;

namespace Aethiumian.AI.Editor.Tests.Navigation
{
    /// <summary>Verifies the temporary backend selection and package movement lifecycle boundary.</summary>
    public sealed class MovementBackendTests
    {
        private MovementBackend.Mode originalBackend;
        private GameObject host;
        private BehaviourTreeData data;

        /// <summary>Preserves the process-global backend before each test.</summary>
        [SetUp]
        public void SetUp()
        {
            originalBackend = MovementBackend.Current;
        }

        /// <summary>Restores global state and destroys test-owned Unity objects.</summary>
        [TearDown]
        public void TearDown()
        {
            MovementBackend.Current = originalBackend;
            if (host)
            {
                UnityEngine.Object.DestroyImmediate(host);
            }

            if (data)
            {
                UnityEngine.Object.DestroyImmediate(data);
            }
        }

        /// <summary>Verifies New smart movement bypasses legacy planning and snapshots backend selection.</summary>
        [UnityTest]
        public IEnumerator NewSmartMovement_SnapshotsBackendAndSkipsLegacyPathProvider()
        {
            host = new GameObject("movement-backend-test");
            TestMovementSource source = host.AddComponent<TestMovementSource>();
            host.AddComponent<Rigidbody2D>();
            host.AddComponent<BoxCollider2D>();
            var movement = new TestMovement
            {
                path = Movement.PathMode.smart,
                type = Movement.Behaviour.fixedDestination,
            };
            var head = new FixedJump { uuid = UUID.NewUUID() };
            data = ScriptableObject.CreateInstance<BehaviourTreeData>();
            data.noActionMaximumDurationLimit = true;
            data.headNodeUUID = head.uuid;
            data.nodes.Add(head);
            var tree = new BehaviourTree(data, host, source);
            movement.behaviourTree = tree;
            int remainingFrames = 120;
            while (!tree.IsInitialized && !tree.IsError && remainingFrames-- > 0)
            {
                yield return null;
            }

            Assert.That(tree.IsError, Is.False);
            Assert.That(tree.IsInitialized, Is.True);

            MovementBackend.Current = MovementBackend.Mode.New;
            movement.Awake();
            MovementBackend.Current = MovementBackend.Mode.Legacy;
            movement.Start();

            Assert.That(movement.NewInitializationCount, Is.EqualTo(1));
            Assert.That(movement.NewStartCount, Is.EqualTo(1));
            Assert.That(movement.LegacyPathFinderCount, Is.Zero);

            source.CanMove = false;
            movement.FixedUpdate();
            Assert.That(movement.NewFixedUpdateCount, Is.Zero,
                "Forbidden intentional movement must not tick the new backend.");
            Assert.That(movement.ForbiddenCount, Is.EqualTo(1));

            source.CanMove = true;
            movement.FixedUpdate();
            Assert.That(movement.NewFixedUpdateCount, Is.EqualTo(1),
                "Changing the global backend after Awake must not change this execution.");
        }

        private sealed class TestMovementSource : MonoBehaviour, IMovementSource
        {
            /// <summary>Gets or sets whether the test movement is permitted.</summary>
            public bool CanMove { get; set; } = true;
        }

        [Serializable]
        private sealed class TestMovement : Movement
        {
            public int NewInitializationCount { get; private set; }
            public int NewStartCount { get; private set; }
            public int NewFixedUpdateCount { get; private set; }
            public int LegacyPathFinderCount { get; private set; }
            public int ForbiddenCount { get; private set; }

            protected override bool SupportsNewBackend => true;

            protected override void InitNewMovement() => NewInitializationCount++;
            protected override void StartNewMovement() => NewStartCount++;
            protected override void NewMovementFixedUpdate() => NewFixedUpdateCount++;
            protected override void OnNewMovementForbidden() => ForbiddenCount++;
            protected override void Toward(Vector2 destination) { }

            protected override PathFinder GetPathFinder()
            {
                LegacyPathFinderCount++;
                return null;
            }

            protected override void StartSmartMoving(PathProvider provider) { }
        }
    }
}
