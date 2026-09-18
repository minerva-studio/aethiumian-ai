using System.Collections;
using Aethiumian.AI.Nodes;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Aethiumian.AI.Editor.Tests
{
    /// <summary>Verifies that MotionFlip observes horizontal motion without owning entity facing.</summary>
    public sealed class MotionFlipTests
    {
        [UnityTest]
        public IEnumerator PositiveHorizontalMotionClearsFlip()
        {
            yield return AssertFlip(new Vector2(2f, 0f), true, false);
        }

        [UnityTest]
        public IEnumerator NegativeHorizontalMotionSetsFlip()
        {
            yield return AssertFlip(new Vector2(-2f, 0f), false, true);
        }

        [UnityTest]
        public IEnumerator ZeroOrVerticalMotionPreservesFlip()
        {
            yield return AssertFlip(Vector2.zero, true, true);
            yield return AssertFlip(new Vector2(0f, 2f), false, false);
        }

        [UnityTest]
        public IEnumerator MissingSpriteRendererFailsWithoutChangingVelocity()
        {
            MotionFlip node = TreeTestFixture.CreateNode<MotionFlip>("Motion flip");
            using TreeTestFixture fixture = TreeTestFixture.Create(node);
            Rigidbody2D body = fixture.GameObject.AddComponent<Rigidbody2D>();
            body.linearVelocity = new Vector2(-2f, 1f);

            yield return fixture.WaitUntilReady();
            fixture.Start();
            fixture.Tick();

            Assert.That(fixture.Tree.MainStack.ReturnValue, Is.False);
            Assert.That(fixture.Tree.IsFaulted, Is.False);
            Assert.That(body.linearVelocity, Is.EqualTo(new Vector2(-2f, 1f)));
        }

        private static IEnumerator AssertFlip(Vector2 velocity, bool initialFlip, bool expectedFlip)
        {
            MotionFlip node = TreeTestFixture.CreateNode<MotionFlip>("Motion flip");
            using TreeTestFixture fixture = TreeTestFixture.Create(node);
            Rigidbody2D body = fixture.GameObject.AddComponent<Rigidbody2D>();
            SpriteRenderer sprite = fixture.GameObject.AddComponent<SpriteRenderer>();
            body.linearVelocity = velocity;
            sprite.flipX = initialFlip;

            yield return fixture.WaitUntilReady();
            fixture.Start();
            fixture.Tick();

            Assert.That(fixture.Tree.MainStack.ReturnValue, Is.True);
            Assert.That(fixture.Tree.IsFaulted, Is.False);
            Assert.That(sprite.flipX, Is.EqualTo(expectedFlip));
            Assert.That(body.linearVelocity, Is.EqualTo(velocity));
        }
    }
}
