using Aethiumian.AI.Editor.Tests.Support;
using Aethiumian.AI.Nodes;
using Aethiumian.AI.Variables;
using NUnit.Framework;
using System.Collections;
using UnityEngine;
using UnityEngine.TestTools;

namespace Aethiumian.AI.Editor.Tests.Execution
{
    /// <summary>Exercises TimerVariable translation through real runtime Subtree construction.</summary>
    public sealed class SubtreeTimerTranslationTests
    {
        /// <summary>Verifies each constructed subtree aliases its mapped parent timer without carrying it across parent rebuilds.</summary>
        [UnityTest]
        public IEnumerator MappedTimer_ReusesParentInstanceAcrossSubtreeConstructionAndParentRebuild()
        {
            VariableData parentTimer = CreateTimer("Parent Timer");
            VariableData childTimer = CreateTimer("Child Timer");
            BehaviourTreeData nestedData = CreateNestedTree(childTimer);
            try
            {
                TimerVariable firstParent = null;
                using (TreeTestFixture first = CreateParentFixture(nestedData, parentTimer, childTimer, mapped: true))
                {
                    yield return first.WaitUntilReady();
                    first.Start();
                    first.Tick();
                    Subtree firstSubtree = first.GetRuntimeNode<Subtree>(first.Data.nodes[0] as Subtree);
                    yield return WaitForInitialized(firstSubtree.RuntimeTree);

                    firstParent = first.Tree.GetVariable(parentTimer.UUID) as TimerVariable;
                    TimerVariable firstChild = firstSubtree.RuntimeTree.GetVariable(childTimer.UUID) as TimerVariable;
                    Assert.That(firstChild, Is.SameAs(firstParent));

                    firstChild.SetValue(8f);
                    Assert.That(firstParent.Remaining, Is.GreaterThan(0f));
                }

                using (TreeTestFixture rebuilt = CreateParentFixture(nestedData, parentTimer, childTimer, mapped: true))
                {
                    yield return rebuilt.WaitUntilReady();
                    rebuilt.Start();
                    rebuilt.Tick();
                    Subtree rebuiltSubtree = rebuilt.GetRuntimeNode<Subtree>(rebuilt.Data.nodes[0] as Subtree);
                    yield return WaitForInitialized(rebuiltSubtree.RuntimeTree);

                    TimerVariable rebuiltParent = rebuilt.Tree.GetVariable(parentTimer.UUID) as TimerVariable;
                    TimerVariable rebuiltChild = rebuiltSubtree.RuntimeTree.GetVariable(childTimer.UUID) as TimerVariable;
                    Assert.That(rebuiltChild, Is.SameAs(rebuiltParent));
                    Assert.That(rebuiltParent, Is.Not.SameAs(firstParent));
                    Assert.That(rebuiltParent.Remaining, Is.Zero);
                }
            }
            finally
            {
                Object.DestroyImmediate(nestedData);
            }
        }

        /// <summary>Verifies an unmapped child timer remains independent of its parent timer.</summary>
        [UnityTest]
        public IEnumerator UnmappedTimer_RemainsIsolatedFromParentTimer()
        {
            VariableData parentTimer = CreateTimer("Parent Timer");
            VariableData childTimer = CreateTimer("Child Timer");
            BehaviourTreeData nestedData = CreateNestedTree(childTimer);
            try
            {
                using TreeTestFixture fixture = CreateParentFixture(nestedData, parentTimer, childTimer, mapped: false);
                yield return fixture.WaitUntilReady();
                fixture.Start();
                fixture.Tick();
                Subtree subtree = fixture.GetRuntimeNode<Subtree>(fixture.Data.nodes[0] as Subtree);
                yield return WaitForInitialized(subtree.RuntimeTree);

                TimerVariable parent = fixture.Tree.GetVariable(parentTimer.UUID) as TimerVariable;
                TimerVariable child = subtree.RuntimeTree.GetVariable(childTimer.UUID) as TimerVariable;
                Assert.That(child, Is.Not.SameAs(parent));

                child.SetValue(8f);
                Assert.That(child.Remaining, Is.GreaterThan(0f));
                Assert.That(parent.Remaining, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(nestedData);
            }
        }

        /// <summary>Builds one nested prototype that has a TimerVariable local definition.</summary>
        private static BehaviourTreeData CreateNestedTree(VariableData childTimer)
        {
            Constant head = TreeTestFixture.CreateNode<Constant>("Nested Head");
            head.returnValue = true;
            BehaviourTreeData data = ScriptableObject.CreateInstance<BehaviourTreeData>();
            data.noActionMaximumDurationLimit = true;
            data.headNodeUUID = head.uuid;
            data.nodes.Add(head);
            data.variables.Add(childTimer);
            return data;
        }

        /// <summary>Builds a parent fixture whose Subtree optionally maps the nested TimerVariable.</summary>
        private static TreeTestFixture CreateParentFixture(BehaviourTreeData nestedData, VariableData parentTimer, VariableData childTimer, bool mapped)
        {
            Subtree subtree = TreeTestFixture.CreateNode<Subtree>("Subtree");
            subtree.behaviourTreeData = nestedData;
            subtree.variableTable = new VariableTableTranslationBuilder
            {
                entries = mapped
                    ? new[] { new VariableTranslationTable.Entry { from = childTimer.UUID, to = parentTimer.UUID } }
                    : new VariableTranslationTable.Entry[0],
            };
            return TreeTestFixture.Create(subtree, new[] { parentTimer });
        }

        /// <summary>Creates an authored local TimerVariable definition with a stable UUID.</summary>
        private static VariableData CreateTimer(string name)
        {
            VariableData data = new(name, VariableType.Float);
            data.Flags |= VariableFlag.Timer;
            return data;
        }

        /// <summary>Waits for the nested BehaviourTree initializer that Subtree.Awake created.</summary>
        private static IEnumerator WaitForInitialized(BehaviourTree tree)
        {
            while (!tree.IsInitialized && !tree.IsFaulted)
            {
                yield return null;
            }

            Assert.That(tree.IsFaulted, Is.False);
            Assert.That(tree.IsInitialized, Is.True);
        }
    }
}
