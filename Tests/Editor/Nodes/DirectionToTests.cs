using Aethiumian.AI.Nodes;
using Aethiumian.AI.Variables;
using NUnit.Framework;
using System.Collections;
using UnityEngine;
using UnityEngine.TestTools;

namespace Aethiumian.AI.Editor.Tests
{
    /// <summary>Verifies DirectionTo reads explicit runtime targets and rejects destroyed objects.</summary>
    public sealed class DirectionToTests
    {
        [UnityTest]
        public IEnumerator ExplicitTarget_UsesBoundPositionInsteadOfPlayer()
        {
            GameObject targetObject = new("DirectionTarget");
            try
            {
                targetObject.transform.position = new Vector3(3f, -4f, 0f);
                VariableData targetData = new("Target", VariableType.UnityObject);
                targetData.SetDefaultValue(targetObject);
                VariableData resultData = new("Direction", VariableType.Vector3);
                resultData.SetDefaultValue(Vector3.zero);

                DirectionTo node = TreeTestFixture.CreateNode<DirectionTo>("Direction to target");
                node.target = new VariableReference();
                node.result = new VariableReference();
                node.target.SetReference(targetData);
                node.result.SetReference(resultData);

                using TreeTestFixture fixture = TreeTestFixture.Create(
                    node,
                    new[] { targetData, resultData });
                fixture.GameObject.transform.position = Vector3.zero;
                yield return fixture.WaitUntilReady();

                fixture.Start();
                fixture.Tick();

                Assert.That(fixture.Tree.MainStack.ReturnValue, Is.True);
                Assert.That(fixture.Tree.Variables[resultData.UUID].Vector3Value,
                    Is.EqualTo(new Vector3(0.6f, -0.8f, 0f)));
            }
            finally
            {
                Object.DestroyImmediate(targetObject);
            }
        }

        [UnityTest]
        public IEnumerator DestroyedObjectTarget_ReturnsFailureWithoutTreeFault()
        {
            GameObject targetObject = new("DestroyedDirectionTarget");
            VariableData targetData = new("Target", VariableType.UnityObject);
            targetData.SetDefaultValue(targetObject);
            VariableData resultData = new("Direction", VariableType.Vector3);
            resultData.SetDefaultValue(Vector3.right);

            DirectionTo node = TreeTestFixture.CreateNode<DirectionTo>("Direction to destroyed target");
            node.target = new VariableReference();
            node.result = new VariableReference();
            node.target.SetReference(targetData);
            node.result.SetReference(resultData);

            using TreeTestFixture fixture = TreeTestFixture.Create(
                node,
                new[] { targetData, resultData });
            yield return fixture.WaitUntilReady();

            Object.DestroyImmediate(targetObject);
            fixture.Start();
            fixture.Tick();

            Assert.That(fixture.Tree.MainStack.ReturnValue, Is.False);
            Assert.That(fixture.Tree.IsFaulted, Is.False);
        }
    }
}
