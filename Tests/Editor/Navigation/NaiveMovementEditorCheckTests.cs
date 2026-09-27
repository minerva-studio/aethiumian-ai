using Aethiumian.AI.Nodes;
using NUnit.Framework;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.TestTools;

namespace Aethiumian.AI.Navigation.Tests
{
    /// <summary>Verifies that Naive authoring rejects goals requiring world queries.</summary>
    public sealed class NaiveMovementEditorCheckTests
    {
        [Test]
        public void NaiveRejectsRetreatAndLineOfSightGoals()
        {
            GameObject prefab = new("naive-editor-check-body");
            BehaviourTreeData tree = ScriptableObject.CreateInstance<BehaviourTreeData>();
            try
            {
                prefab.AddComponent<Rigidbody2D>();
                prefab.AddComponent<BoxCollider2D>();
                tree.prefab = prefab;

                Walk retreat = new() { path = Movement.PathMode.Naive, type = Movement.Behaviour.Retreat };
                Fly confront = new() { path = Movement.PathMode.Naive, type = Movement.Behaviour.Trace, goal = MovementGoal.Confront };
                Jump firing = new() { path = Movement.PathMode.Naive, type = Movement.Behaviour.Trace, goal = MovementGoal.FiringPosition };
                LogAssert.Expect(LogType.Error, new Regex("Naive Walk cannot retreat"));
                Assert.That(retreat.EditorCheck(tree), Is.False);
                LogAssert.Expect(LogType.Error, new Regex("Naive Fly cannot retreat or use line-of-sight goals"));
                Assert.That(confront.EditorCheck(tree), Is.False);
                LogAssert.Expect(LogType.Error, new Regex("Naive Jump cannot retreat or use line-of-sight goals"));
                Assert.That(firing.EditorCheck(tree), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(tree);
                Object.DestroyImmediate(prefab);
            }
        }
    }
}
