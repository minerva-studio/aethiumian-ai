using Aethiumian.AI;
using Aethiumian.AI.Navigation;
using Aethiumian.AI.Nodes;
using Aethiumian.AI.References;
using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.TestTools;

namespace Aethiumian.AI.Navigation.Tests
{
    /// <summary>Verifies Sprint force application and movement-state lifetime through its action API.</summary>
    public sealed class SprintActionTests
    {
        private readonly List<RuntimeFixture> fixtures = new();
        private SimulationMode2D previousSimulationMode;

        /// <summary>Runs physics explicitly so each requested force application has one observed step.</summary>
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousSimulationMode = Physics2D.simulationMode;
            Physics2D.simulationMode = SimulationMode2D.Script;
            yield return null;
        }

        /// <summary>Stops active test actions and restores physics settings after every case.</summary>
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (RuntimeFixture fixture in fixtures)
            {
                fixture.Dispose();
            }

            fixtures.Clear();
            Physics2D.simulationMode = previousSimulationMode;
            yield return null;
        }

        /// <summary>Verifies Once submits one force while holding Sprinting until its duration ends.</summary>
        [UnityTest]
        public IEnumerator Once_AppliesOneForceAndReportsSprintThenIdle()
        {
            RuntimeFixture fixture = CreateFixture(Sprint.ForceApplication.Once);
            yield return fixture.WaitUntilInitialized();
            Sprint action = fixture.RuntimeAction;
            float deltaTime = Time.fixedDeltaTime;

            Assert.That(action.Execute(), Is.EqualTo(State.WaitAction));
            Assert.That(fixture.Source.States, Is.EqualTo(new[] { MovementState.Sprinting }));

            Simulate(deltaTime);
            Assert.That(fixture.Body.linearVelocity.x, Is.EqualTo(10f * deltaTime).Within(0.0001f));

            action.FixedUpdate();
            Assert.That(action.IsComplete, Is.False);
            Simulate(deltaTime);
            Assert.That(fixture.Body.linearVelocity.x, Is.EqualTo(10f * deltaTime).Within(0.0001f));

            action.FixedUpdate();
            Assert.That(action.IsComplete, Is.True);
            Assert.That(fixture.Source.States, Is.EqualTo(new[] { MovementState.Sprinting, MovementState.Idle }));
            Simulate(deltaTime);
            Assert.That(fixture.Body.linearVelocity.x, Is.EqualTo(10f * deltaTime).Within(0.0001f));
        }

        /// <summary>Verifies Repeated submits one force on each active fixed step and then returns Idle.</summary>
        [UnityTest]
        public IEnumerator Repeated_AppliesForceOnEachActiveFixedStepThenReportsIdle()
        {
            RuntimeFixture fixture = CreateFixture(Sprint.ForceApplication.Repeated);
            yield return fixture.WaitUntilInitialized();
            Sprint action = fixture.RuntimeAction;
            float deltaTime = Time.fixedDeltaTime;

            Assert.That(action.Execute(), Is.EqualTo(State.WaitAction));
            Assert.That(fixture.Body.linearVelocity, Is.EqualTo(Vector2.zero));
            Assert.That(fixture.Source.States, Is.Empty);

            action.FixedUpdate();
            Simulate(deltaTime);
            Assert.That(fixture.Body.linearVelocity.x, Is.EqualTo(10f * deltaTime).Within(0.0001f));

            action.FixedUpdate();
            Assert.That(action.IsComplete, Is.True);
            Assert.That(fixture.Source.States, Is.EqualTo(new[] { MovementState.Sprinting, MovementState.Idle }));
            Simulate(deltaTime);
            Assert.That(fixture.Body.linearVelocity.x, Is.EqualTo(20f * deltaTime).Within(0.0001f));

            action.FixedUpdate();
            Simulate(deltaTime);
            Assert.That(fixture.Body.linearVelocity.x, Is.EqualTo(20f * deltaTime).Within(0.0001f));
        }

        /// <summary>Verifies loss of movement permission aborts either strategy and reports Idle.</summary>
        [UnityTest]
        public IEnumerator MovementPermissionLoss_StopsFurtherForceAndReturnsIdle()
        {
            yield return VerifyMovementPermissionLoss(Sprint.ForceApplication.Once);
            yield return VerifyMovementPermissionLoss(Sprint.ForceApplication.Repeated);
        }

        private IEnumerator VerifyMovementPermissionLoss(Sprint.ForceApplication application)
        {
            RuntimeFixture fixture = CreateFixture(application);
            yield return fixture.WaitUntilInitialized();
            Sprint action = fixture.RuntimeAction;
            float deltaTime = Time.fixedDeltaTime;

            Assert.That(action.Execute(), Is.EqualTo(State.WaitAction));
            if (application == Sprint.ForceApplication.Repeated)
            {
                action.FixedUpdate();
            }

            Simulate(deltaTime);
            float velocityAfterFirstApplication = fixture.Body.linearVelocity.x;
            Assert.That(velocityAfterFirstApplication, Is.EqualTo(10f * deltaTime).Within(0.0001f));

            fixture.Source.CanMove = false;
            action.FixedUpdate();
            Assert.That(action.IsComplete, Is.True);
            Assert.That(fixture.Source.States, Is.EqualTo(new[] { MovementState.Sprinting, MovementState.Idle }));
            Simulate(deltaTime);
            Assert.That(fixture.Body.linearVelocity.x, Is.EqualTo(velocityAfterFirstApplication).Within(0.0001f));
        }

        private RuntimeFixture CreateFixture(Sprint.ForceApplication application)
        {
            GameObject host = new("SprintActionTestHost");
            var source = host.AddComponent<TestMovementSource>();
            Rigidbody2D body = host.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.linearDamping = 0f;
            body.mass = 1f;

            var prototype = new Sprint
            {
                name = "Sprint Test",
                uuid = UUID.NewUUID(),
                parent = NodeReference.Empty,
                duration = Time.fixedDeltaTime * 2f,
                force = new Vector2(10f, 0f),
                forceApplication = application,
            };
            BehaviourTreeData data = ScriptableObject.CreateInstance<BehaviourTreeData>();
            data.noActionMaximumDurationLimit = true;
            data.headNodeUUID = prototype.uuid;
            data.nodes.Add(prototype);

            var tree = new Aethiumian.AI.BehaviourTree(data, host, source);
            var fixture = new RuntimeFixture(host, data, tree, source, body, prototype);
            fixtures.Add(fixture);
            return fixture;
        }

        private static void Simulate(float deltaTime)
        {
            Assert.That(Physics2D.Simulate(deltaTime), Is.True);
        }

        private sealed class RuntimeFixture
        {
            private readonly Aethiumian.AI.BehaviourTree tree;
            private readonly Sprint prototype;

            public GameObject Host { get; }
            public BehaviourTreeData Data { get; }
            public TestMovementSource Source { get; }
            public Rigidbody2D Body { get; }
            public Sprint RuntimeAction => (Sprint)tree.References[prototype.uuid];

            public RuntimeFixture(
                GameObject host,
                BehaviourTreeData data,
                Aethiumian.AI.BehaviourTree tree,
                TestMovementSource source,
                Rigidbody2D body,
                Sprint prototype)
            {
                Host = host;
                Data = data;
                this.tree = tree;
                Source = source;
                Body = body;
                this.prototype = prototype;
            }

            public IEnumerator WaitUntilInitialized()
            {
                float deadline = Time.realtimeSinceStartup + 5f;
                while (!tree.IsInitialized && !tree.IsFaulted && Time.realtimeSinceStartup < deadline)
                {
                    yield return null;
                }

                Assert.That(tree.IsFaulted, Is.False, "The runtime tree failed to initialize.");
                Assert.That(tree.IsInitialized, Is.True, "The runtime tree did not initialize in time.");
            }

            public void Dispose()
            {
                if (tree.IsInitialized && tree.References.TryGetValue(prototype.uuid, out TreeNode runtimeNode)
                    && runtimeNode is Sprint action)
                {
                    action.OnDestroy();
                }

                if (Host) Object.Destroy(Host);
                if (Data) Object.Destroy(Data);
            }
        }

        private sealed class TestMovementSource : MonoBehaviour, IMovementSource
        {
            public readonly List<MovementState> States = new();
            public bool CanMove { get; set; } = true;

            public void SetMovementState(MovementStateInfo stateInfo)
            {
                States.Add(stateInfo.State);
            }
        }
    }
}
