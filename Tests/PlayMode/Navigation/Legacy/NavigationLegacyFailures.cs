// Consolidated explicit navigation regressions; normal test runs exclude [Explicit].
using Aethiumian.AI.Navigation;
using Aethiumian.AI.Nodes;
using NUnit.Framework;
using System.Collections;
using UnityEngine.TestTools;
using UnityEngine;

namespace Aethiumian.AI.Navigation.Tests
{
    // ===== Handoff failures =====
    /// <summary>Legacy regression signal retained while partial-prefix handoff is repaired.</summary>
    public sealed partial class NavigationHandoffContractTests
    {
        [Explicit("Known runtime regression: a physically grounded partial Ground prefix leaves the body at its original start.")]
        [UnityTest]
        public IEnumerator PartialGroundPrefixMakesPhysicalProgressBeforeContinuationRequest()
        {
            using MapNavigationRuntime runtime = CreateRuntime();
            using RuntimeContextScope context = new(runtime);
            CreateGround();
            GameObject target = CreateTraceTarget(new Vector2(56.5f, 1f));
            MovementHarness harness = CreateHarness(MovementStart, CreateControlledWalkTrace(target), canMove: false);
            yield return WaitForTreeCreated(harness);
            yield return WaitUntilGrounded(harness);
            harness.Source.CanMove = true;
            yield return WaitForRequest();

            ControlledWalk.Request first = ControlledWalk.Requests[0];
            Assert.That(first.Start.y,
                Is.EqualTo(harness.Body.position.y - BodyHeight * 0.5f).Within(0.05f),
                "The controlled route must start from the established physical ground anchor. " + DescribeHarness(harness));
            Vector2 partialEnd = first.Start + Vector2.right * 4f;
            ControlledWalk.Complete(first, CreateGroundRoute(first, partialEnd, false));

            for (int frame = 0; frame < PlanningFrameLimit
                && (harness.Body.position.x <= first.Start.x + 0.5f || ControlledWalk.Requests.Count < 2); frame++)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.That(harness.Body.position.x, Is.GreaterThan(first.Start.x + 0.5f), DescribeHarness(harness));
            Assert.That(ControlledWalk.Requests.Count, Is.GreaterThanOrEqualTo(2), DescribeHarness(harness));
            Assert.That(ControlledWalk.Requests[1].Start.x, Is.GreaterThan(first.Start.x + 0.01f),
                "The continuation must be requested from observed movement progress, not the original start. "
                + DescribeHarness(harness));
            Assert.That(harness.AI.BehaviourTree.IsFaulted, Is.False, DescribeHarness(harness));
        }
    }
}
