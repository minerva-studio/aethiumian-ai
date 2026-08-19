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

namespace Aethiumian.AI.Editor.Tests.Graph
{
    /// <summary>
    /// EditMode coverage for graph topology and non-dirty layout resolution.
    /// </summary>
    /// <summary>Graph Editor GraphCompositePresentation contract tests.</summary>
    [Category("GraphEditor")]
    public sealed class GraphCompositePresentationTests : GraphEditorTestFixture
    {
        [Test]
        public void GraphAppearance_CompositeFamiliesUseDistinctFallbackStrokes()
        {
            GraphCanvasAppearance appearance = new();
            Assert.That(GraphCanvasAppearance.GetFamily(Node<Sequence>("Sequence")), Is.EqualTo(GraphVisualFamily.Sequence));
            Assert.That(GraphCanvasAppearance.GetFamily(Node<Loop>("Loop")), Is.EqualTo(GraphVisualFamily.Loop));
            Assert.That(GraphCanvasAppearance.GetFamily(Node<Condition>("Condition")), Is.EqualTo(GraphVisualFamily.Condition));
            Assert.That(GraphCanvasAppearance.GetFamily(Node<Decision>("Decision")), Is.EqualTo(GraphVisualFamily.Decision));
            Assert.That(GraphCanvasAppearance.GetFamily(Node<Probability>("Probability")), Is.EqualTo(GraphVisualFamily.Probability));
            Assert.That(GraphCanvasAppearance.GetFamily(Node<Parallel>("Parallel")), Is.EqualTo(GraphVisualFamily.Parallel));
            Assert.That(appearance.GetFamilyStroke(GraphVisualFamily.Condition), Is.Not.EqualTo(appearance.GetFamilyStroke(GraphVisualFamily.Decision)));
            Assert.That(appearance.GetFamilyStroke(GraphVisualFamily.Loop), Is.EqualTo(new Color(71f / 255f, 209f / 255f, 184f / 255f, 1f)));
            Assert.That(appearance.GetFamilyStroke(GraphVisualFamily.Condition), Is.EqualTo(new Color(184f / 255f, 122f / 255f, 235f / 255f, 1f)));
            Assert.That(appearance.GetFamilyStroke(GraphVisualFamily.Decision), Is.EqualTo(new Color(126f / 255f, 138f / 255f, 242f / 255f, 1f)));
            Assert.That(appearance.GetFamilyStroke(GraphVisualFamily.Probability), Is.EqualTo(new Color(232f / 255f, 111f / 255f, 154f / 255f, 1f)));
            Assert.That(appearance.GetFamilyStroke(GraphVisualFamily.Parallel), Is.EqualTo(new Color(89f / 255f, 168f / 255f, 242f / 255f, 1f)));
            Assert.That(appearance.GetFamilyFill(GraphVisualFamily.Condition, true).a, Is.EqualTo(0.12f));
            Assert.That(appearance.GetFamilyFill(GraphVisualFamily.Condition, false).a, Is.EqualTo(0.08f));
        }

        [Test]
        public void GraphAppearance_StructuralOwnerColorsNestedContinuationAndPreservesSpecialEdges()
        {
            Sequence sequence = Node<Sequence>("Sequence");
            Condition condition = Node<Condition>("Condition");
            TestNode predicate = Node<TestNode>("Predicate");
            TestNode getValue = Node<TestNode>("Get Value");
            TestNode wait = Node<TestNode>("Wait");
            TestHost host = Node<TestHost>("Host");
            TestService service = Node<TestService>("Service");
            TestNode rawTarget = Node<TestNode>("Raw Target");
            TestNode plain = Node<TestNode>("Plain");
            TestNode plainChild = Node<TestNode>("Plain Child");
            sequence.events = new[] { condition.ToReference(), getValue.ToReference(), wait.ToReference() };
            condition.condition = predicate.ToReference();
            condition.parent = sequence.ToReference();
            getValue.parent = sequence.ToReference();
            wait.parent = sequence.ToReference();
            predicate.parent = condition.ToReference();
            host.services = new List<NodeReference> { service.ToReference() };
            host.raw = new RawNodeReference { UUID = rawTarget.uuid };
            plain.child = plainChild.ToReference();
            BehaviourTreeData tree = Tree(
                sequence, condition, predicate, getValue, wait,
                host, service, rawTarget, plain, plainChild);
            GraphPresentation presentation = GraphPresentationBuilder.Build(GraphTopologyBuilder.Build(tree, includeRawReferences: true));
            GraphCanvasAppearance appearance = new();
            GraphEdgeLayerElement edges = new(appearance);
            edges.SetPresentation(presentation, Array.Empty<GraphPortDescriptor>());
            GraphPresentationItem sequenceItem = presentation.Find(sequence.uuid);
            GraphPresentationItem conditionItem = presentation.Find(condition.uuid);

            GraphPresentationRelation afterCondition = presentation.Relations.Single(relation =>
                relation.Kind == GraphPresentationRelationKind.SequenceNext
                && relation.Source.Item == conditionItem
                && relation.Target.Item == presentation.Find(getValue.uuid));
            GraphPresentationRelation afterGetValue = presentation.Relations.Single(relation =>
                relation.Kind == GraphPresentationRelationKind.SequenceNext
                && relation.Source.Item == presentation.Find(getValue.uuid));
            GraphPresentationRelation sequenceCompletion = presentation.Relations.Single(relation =>
                relation.Kind == GraphPresentationRelationKind.SequenceSuccess
                && relation.Source.Item == presentation.Find(wait.uuid)
                && relation.Target == sequenceItem.FlowComplete);
            GraphPresentationRelation[] conditionRelations = presentation.Relations.Where(relation =>
                relation.VisualOwner == conditionItem).ToArray();
            GraphPresentationRelation serviceRelation = presentation.Relations.Single(relation => relation.Kind == GraphPresentationRelationKind.Service);
            GraphPresentationRelation rawRelation = presentation.Relations.Single(relation => relation.Kind == GraphPresentationRelationKind.Raw);
            GraphPresentationRelation plainRelation = presentation.Relations.Single(relation =>
                relation.Kind == GraphPresentationRelationKind.Structural
                && relation.Source.Item == presentation.Find(plain.uuid));

            Color sequenceColor = appearance.GetFamilyStroke(GraphVisualFamily.Sequence);
            Color conditionColor = appearance.GetFamilyStroke(GraphVisualFamily.Condition);
            Assert.That(afterCondition.VisualOwner, Is.SameAs(sequenceItem));
            Assert.That(afterGetValue.VisualOwner, Is.SameAs(sequenceItem));
            Assert.That(edges.GetRenderedColor(afterCondition), Is.EqualTo(sequenceColor));
            Assert.That(edges.GetRenderedColor(afterGetValue), Is.EqualTo(sequenceColor));
            Assert.That(edges.GetRenderedColor(sequenceCompletion), Is.EqualTo(sequenceColor));
            Assert.That(conditionRelations, Is.Not.Empty);
            Assert.That(conditionRelations.All(relation => edges.GetRenderedColor(relation) == conditionColor), Is.True);
            Assert.That(conditionRelations.Any(relation => relation.Role == GraphPresentationRelationRole.PlaceholderHint), Is.True);
            Assert.That(edges.GetRenderedColor(serviceRelation), Is.EqualTo(appearance.ServiceEdge));
            Assert.That(edges.GetRenderedColor(rawRelation), Is.EqualTo(appearance.RawEdge));
            Assert.That(plainRelation.VisualOwner, Is.Null);
            Assert.That(edges.GetRenderedColor(plainRelation), Is.EqualTo(appearance.GetFamilyStroke(GraphVisualFamily.Neutral)));
            Assert.That(afterCondition.ContextualOwner, Is.Null);
        }

        [Test]
        public void GraphAppearance_StructuralOwnerCoversEveryCompositeFamily()
        {
            Sequence sequence = Node<Sequence>("Sequence");
            Loop loop = Node<Loop>("Loop");
            Condition condition = Node<Condition>("Condition");
            Decision decision = Node<Decision>("Decision");
            Probability probability = Node<Probability>("Probability");
            PseudoProbability pseudo = Node<PseudoProbability>("Pseudo Probability");
            Parallel parallel = Node<Parallel>("Parallel");
            ForEach forEach = Node<ForEach>("For Each");
            BehaviourTreeData tree = Tree(sequence, loop, condition, decision, probability, pseudo, parallel, forEach);
            GraphPresentation presentation = GraphPresentationBuilder.Build(GraphTopologyBuilder.Build(tree));
            GraphCanvasAppearance appearance = new();
            GraphEdgeLayerElement edges = new(appearance);
            edges.SetPresentation(presentation, Array.Empty<GraphPortDescriptor>());

            foreach (TreeNode node in new TreeNode[] { sequence, loop, condition, decision, probability, pseudo, parallel, forEach })
            {
                GraphPresentationItem owner = presentation.Find(node.uuid);
                GraphPresentationRelation[] owned = presentation.Relations.Where(relation => relation.VisualOwner == owner).ToArray();
                Color expected = appearance.GetFamilyStroke(GraphCanvasAppearance.GetFamily(node));
                Assert.That(owned, Is.Not.Empty, node.GetType().Name);
                Assert.That(owned.All(relation => edges.GetRenderedColor(relation) == expected), Is.True, node.GetType().Name);
            }
        }

        [Test]
        public void GraphAppearance_MissingCustomStylesUseNonZeroFallbacks()
        {
            GraphCanvasAppearance appearance = new();

            appearance.Resolve(null);

            Assert.That(appearance.HasResolvedCustomStyles, Is.False);
            Assert.That(appearance.FlowEdge, Is.EqualTo(new Color(0.25f, 0.72f, 0.92f, 1f)));
            Assert.That(appearance.NodeLineWidth, Is.EqualTo(1.5f));
            Assert.That(appearance.AuthoredLineWidth, Is.EqualTo(2f));
            Assert.That(appearance.DerivedMarkLength, Is.EqualTo(8f));
            Assert.That(appearance.PlaceholderGapLength, Is.EqualTo(6f));
        }

        [Test]
        public void Presentation_ProbabilityConvergesEligibleWeightedBranchesBeforeOuterNext()
        {
            Sequence outer = Node<Sequence>("Outer");
            TestNode before = Node<TestNode>("Before");
            Probability probability = Node<Probability>("Probability");
            TestNode enabled = Node<TestNode>("Enabled");
            TestNode disabled = Node<TestNode>("Disabled");
            TestNode after = Node<TestNode>("After");
            outer.events = new[] { before.ToReference(), probability.ToReference(), after.ToReference() };
            probability.events = new[]
            {
                new Probability.EventWeight { weight = 3, reference = enabled.ToReference() },
                new Probability.EventWeight { weight = 0, reference = disabled.ToReference() },
            };
            BehaviourTreeData tree = Tree(outer, before, probability, enabled, disabled, after);

            GraphPresentation presentation = GraphPresentationBuilder.Build(GraphTopologyBuilder.Build(tree));
            GraphPresentationItem probabilityItem = presentation.Find(probability.uuid);
            GraphPresentationRelation[] candidates = presentation.Relations.Where(relation =>
                relation.Kind == GraphPresentationRelationKind.ProbabilityBranch).ToArray();
            GraphPresentationRelation[] completions = presentation.Relations.Where(relation =>
                relation.Role == GraphPresentationRelationRole.DerivedCompletion
                && relation.Target == probabilityItem.FlowComplete).ToArray();
            GraphPresentationRelation continuation = presentation.Relations.Single(relation =>
                relation.Kind == GraphPresentationRelationKind.SequenceNext
                && relation.Target.Item?.Node?.Node == after);

            Assert.That(probabilityItem.ProbabilityScope, Is.Not.Null);
            Assert.That(probabilityItem.ProbabilityScope.Subtitle, Is.EqualTo("PICK ONE"));
            Assert.That(candidates.Select(relation => relation.Label), Is.EqualTo(new[]
            {
                "Option 1 · Weight 3 · 100%",
                "Option 2 · Weight 0 · 0% · Disabled",
            }));
            Assert.That(candidates.Single(relation => relation.TargetUUID == disabled.uuid).IsVisuallyDisabled, Is.True);
            Assert.That(candidates.All(relation => relation.IsEditableReference), Is.True);
            Assert.That(completions.Length, Is.EqualTo(1));
            Assert.That(completions[0].Source.Item.Node.Node, Is.SameAs(enabled));
            Assert.That(continuation.Source, Is.EqualTo(probabilityItem.FlowComplete));
            Assert.That(presentation.Relations.Any(relation =>
                relation.Source == probabilityItem.Output
                && relation.Target.Item?.Node?.Node == after), Is.False);
        }

        [Test]
        public void Presentation_ProbabilityAllZeroWeightsUseUniformFallback()
        {
            Probability probability = Node<Probability>("Probability");
            TestNode first = Node<TestNode>("First");
            TestNode second = Node<TestNode>("Second");
            probability.events = new[]
            {
                new Probability.EventWeight { weight = 0, reference = first.ToReference() },
                new Probability.EventWeight { weight = -5, reference = second.ToReference() },
            };
            BehaviourTreeData tree = Tree(probability, first, second);

            GraphPresentation presentation = GraphPresentationBuilder.Build(GraphTopologyBuilder.Build(tree));
            GraphPresentationItem owner = presentation.Find(probability.uuid);
            GraphPresentationRelation[] candidates = presentation.Relations.Where(relation =>
                relation.Kind == GraphPresentationRelationKind.ProbabilityBranch).ToArray();

            Assert.That(candidates.Select(relation => relation.Label), Is.EqualTo(new[]
            {
                "Option 1 · Uniform fallback",
                "Option 2 · Uniform fallback",
            }));
            Assert.That(candidates.All(relation => !relation.IsVisuallyDisabled), Is.True);
            Assert.That(presentation.Relations.Count(relation =>
                relation.Role == GraphPresentationRelationRole.DerivedCompletion
                && relation.Target == owner.FlowComplete), Is.EqualTo(2));
        }

        [Test]
        public void Presentation_PseudoProbabilityDescribesDynamicWeightsWithoutStaticPercentages()
        {
            PseudoProbability probability = Node<PseudoProbability>("Pseudo");
            TestNode dynamicTarget = Node<TestNode>("Dynamic");
            TestNode constantTarget = Node<TestNode>("Constant");
            TestNode missingTarget = Node<TestNode>("Missing Variable");
            VariableData dynamicWeight = new("Combat Weight", VariableType.Int);
            VariableData missingWeight = new("Detached Weight", VariableType.Int);
            VariableField<int> dynamicField = new();
            VariableField<int> missingField = new();
            dynamicField.SetReference(dynamicWeight);
            missingField.SetReference(missingWeight);
            probability.maxConsecutiveBranch = 2;
            probability.events = new[]
            {
                new PseudoProbability.EventWeight { weight = dynamicField, reference = dynamicTarget.ToReference() },
                new PseudoProbability.EventWeight { weight = 0, reference = constantTarget.ToReference() },
                new PseudoProbability.EventWeight { weight = missingField, reference = missingTarget.ToReference() },
            };
            BehaviourTreeData tree = Tree(probability, dynamicTarget, constantTarget, missingTarget);
            tree.variables.Add(dynamicWeight);

            GraphPresentation presentation = GraphPresentationBuilder.Build(GraphTopologyBuilder.Build(tree));
            GraphPresentationItem owner = presentation.Find(probability.uuid);
            GraphPresentationRelation[] candidates = presentation.Relations.Where(relation =>
                relation.Kind == GraphPresentationRelationKind.ProbabilityBranch).ToArray();

            Assert.That(owner.ProbabilityScope.Subtitle, Is.EqualTo("PICK ONE · MAX STREAK 2"));
            Assert.That(candidates.Select(relation => relation.Label), Is.EqualTo(new[]
            {
                "Option 1 · Weight · Combat Weight",
                "Option 2 · Weight 0",
                "Option 3 · Weight · MISSING",
            }));
            Assert.That(candidates.All(relation => !relation.IsVisuallyDisabled), Is.True);
            Assert.That(presentation.Relations.Count(relation =>
                relation.Role == GraphPresentationRelationRole.DerivedCompletion
                && relation.Target == owner.FlowComplete), Is.EqualTo(3));
            Assert.That(owner.Node.Warning, Does.Contain(missingWeight.UUID.ToString()));
        }

        [Test]
        public void Presentation_ProbabilityInvalidOptionsDoNotCreateFalseCompletions()
        {
            Probability probability = Node<Probability>("Probability");
            UUID missingUUID = UUID.NewUUID();
            probability.events = new[]
            {
                new Probability.EventWeight { weight = 1, reference = NodeReference.Empty },
                new Probability.EventWeight { weight = 1, reference = new NodeReference(missingUUID) },
            };
            BehaviourTreeData tree = Tree(probability);
            GraphTopology topology = GraphTopologyBuilder.Build(tree);

            GraphPresentation presentation = GraphPresentationBuilder.Build(topology);
            GraphPresentationItem owner = presentation.Find(probability.uuid);
            GraphPresentationItem[] placeholders = presentation.Roots.Where(item =>
                item.ProbabilityPlaceholder != null).ToArray();

            Assert.That(placeholders.Select(item => item.ProbabilityPlaceholder.Title), Is.EqualTo(new[]
            {
                "EMPTY OPTION [0]",
                "MISSING OPTION [1]",
            }));
            Assert.That(placeholders.All(item => item.ProbabilityPlaceholder.IsInvalidSelection), Is.True);
            Assert.That(presentation.Relations.Count(relation =>
                relation.Role == GraphPresentationRelationRole.PlaceholderHint), Is.EqualTo(2));
            Assert.That(presentation.Relations.Any(relation =>
                relation.Role == GraphPresentationRelationRole.DerivedCompletion
                && relation.Target == owner.FlowComplete), Is.False);
            Assert.That(topology.FindNode(probability.uuid).HasWarning, Is.True);
            Assert.That(GraphLayoutResolver.CreateLayout(topology).Positions.Any(entry => entry.UUID == UUID.Empty), Is.False);
        }

        [Test]
        public void Presentation_ProbabilityNoOptionsReturnsFailedThroughCompletion()
        {
            Probability probability = Node<Probability>("Probability");
            BehaviourTreeData tree = Tree(probability);

            GraphPresentation presentation = GraphPresentationBuilder.Build(GraphTopologyBuilder.Build(tree));
            GraphPresentationItem owner = presentation.Find(probability.uuid);
            GraphPresentationItem placeholder = presentation.Roots.Single(item =>
                item.ProbabilityPlaceholder?.Kind == GraphProbabilityPlaceholderKind.NoOptions);

            Assert.That(placeholder.ProbabilityPlaceholder.Subtitle, Is.EqualTo("Returns Failed"));
            Assert.That(presentation.Relations.Single(relation =>
                relation.Source.Item == placeholder
                && relation.Target == owner.FlowComplete).Role,
                Is.EqualTo(GraphPresentationRelationRole.DerivedCompletion));
        }

        [Test]
        public void Presentation_ProbabilityNestedAndDuplicateCandidatesKeepCompletionSemantics()
        {
            Probability probability = Node<Probability>("Probability");
            Sequence nested = Node<Sequence>("Nested");
            TestNode leaf = Node<TestNode>("Leaf");
            nested.events = new[] { leaf.ToReference() };
            probability.events = new[]
            {
                new Probability.EventWeight { weight = 1, reference = nested.ToReference() },
                new Probability.EventWeight { weight = 1, reference = nested.ToReference() },
            };
            BehaviourTreeData tree = Tree(probability, nested, leaf);

            GraphPresentation presentation = GraphPresentationBuilder.Build(GraphTopologyBuilder.Build(tree));
            GraphPresentationItem owner = presentation.Find(probability.uuid);
            GraphPresentationRelation[] authored = presentation.Relations.Where(relation =>
                relation.Kind == GraphPresentationRelationKind.ProbabilityBranch
                && relation.TargetUUID == nested.uuid).ToArray();
            GraphPresentationRelation[] derived = presentation.Relations.Where(relation =>
                relation.Role == GraphPresentationRelationRole.DerivedCompletion
                && relation.Target == owner.FlowComplete).ToArray();

            Assert.That(authored.Length, Is.EqualTo(2));
            Assert.That(derived.Length, Is.EqualTo(2));
            Assert.That(derived.All(relation => relation.Source.Item.Node.Node == nested), Is.True);
            Assert.That(derived.All(relation => relation.Source.Anchor == GraphPresentationAnchorKind.FlowComplete), Is.True);
            Assert.That(authored.Select(relation => relation.OccurrenceId),
                Is.EquivalentTo(derived.Select(relation => relation.OccurrenceId)));
            Assert.That(presentation.Roots.Count(item => item.Node?.Node == nested), Is.EqualTo(1));
        }

        [Test]
        public void Presentation_WhileLoopUsesRepeatAndCompletionBeforeOuterNext()
        {
            Sequence outer = Node<Sequence>("Outer");
            Loop loop = Node<Loop>("Loop");
            TestNode condition = Node<TestNode>("Condition");
            TestNode body = Node<TestNode>("Body");
            TestNode after = Node<TestNode>("After");
            outer.events = new[] { loop.ToReference(), after.ToReference() };
            loop.loopType = Loop.LoopType.@while;
            loop.condition = condition.ToReference();
            loop.events = new[] { body.ToReference() };
            BehaviourTreeData tree = Tree(outer, loop, condition, body, after);

            GraphPresentation presentation = GraphPresentationBuilder.Build(GraphTopologyBuilder.Build(tree));
            GraphPresentationItem loopItem = presentation.Find(loop.uuid);
            GraphPresentationRelation conditionRelation = presentation.Relations.Single(relation =>
                relation.Kind == GraphPresentationRelationKind.LoopCondition
                && relation.Target.Item?.Node?.Node == condition);
            GraphPresentationRelation bodyRelation = presentation.Relations.Single(relation =>
                relation.Kind == GraphPresentationRelationKind.LoopBody
                && relation.Target.Item?.Node?.Node == body);
            GraphPresentationRelation exit = presentation.Relations.Single(relation =>
                relation.Kind == GraphPresentationRelationKind.LoopExit);
            GraphPresentationRelation continuation = presentation.Relations.Single(relation =>
                relation.Kind == GraphPresentationRelationKind.SequenceNext
                && relation.Target.Item?.Node?.Node == after);

            Assert.That(loopItem.Kind, Is.EqualTo(GraphPresentationKind.Loop));
            Assert.That(loopItem.LoopScope.Mode, Is.EqualTo(Loop.LoopType.@while));
            Assert.That(conditionRelation.Role, Is.EqualTo(GraphPresentationRelationRole.AuthoredReference));
            Assert.That(bodyRelation.Role, Is.EqualTo(GraphPresentationRelationRole.AuthoredReference));
            Assert.That(conditionRelation.IsEditableReference, Is.True);
            Assert.That(bodyRelation.IsEditableReference, Is.True);
            Assert.That(presentation.Relations.Count(relation =>
                relation.Kind == GraphPresentationRelationKind.LoopRepeat
                && relation.Role == GraphPresentationRelationRole.DerivedControl), Is.EqualTo(1));
            Assert.That(exit.Role, Is.EqualTo(GraphPresentationRelationRole.DerivedCompletion));
            Assert.That(exit.Target, Is.EqualTo(loopItem.FlowComplete));
            Assert.That(continuation.Source, Is.EqualTo(loopItem.FlowComplete));
            Assert.That(presentation.Relations.Where(relation =>
                relation.Role == GraphPresentationRelationRole.DerivedControl)
                .All(relation => !relation.IsEditableReference), Is.True);
        }

        [Test]
        public void Presentation_WhileLoopEmbedsDecoratorConditionPredicate()
        {
            Loop loop = Node<Loop>("Loop");
            Inverter inverter = Node<Inverter>("Inverter");
            Aethiumian.AI.Nodes.Boolean boolean = Node<Aethiumian.AI.Nodes.Boolean>("Is Ready");
            TestNode body = Node<TestNode>("Body");
            loop.loopType = Loop.LoopType.@while;
            loop.condition = inverter.ToReference();
            loop.events = new[] { body.ToReference() };
            inverter.node = boolean.ToReference();
            inverter.parent = loop.ToReference();
            boolean.parent = inverter.ToReference();
            body.parent = loop.ToReference();
            BehaviourTreeData tree = Tree(loop, inverter, boolean, body);
            tree.GraphLayout = GraphLayoutData.Create(new[]
            {
                new GraphLayoutEntry(loop.uuid, new Vector2(100f, 80f)),
                new GraphLayoutEntry(inverter.uuid, new Vector2(900f, 700f)),
                new GraphLayoutEntry(boolean.uuid, new Vector2(-600f, 420f)),
                new GraphLayoutEntry(body.uuid, new Vector2(-1800f, 4200f)),
            });
            GraphTopology topology = GraphTopologyBuilder.Build(tree);
            Assert.That(topology.Edges.Any(edge => edge.Source.Node == loop
                && edge.Target?.Node == inverter
                && edge.FieldName == nameof(loop.condition)), Is.True);
            GraphLayoutResolver.Resolve(tree, topology);

            GraphPresentation presentation = GraphPresentationBuilder.Build(topology);
            GraphPresentationLayout.Layout(presentation);

            GraphPresentationItem loopItem = presentation.Find(loop.uuid);
            GraphLoopScope scope = presentation.Find(loop.uuid).LoopScope;
            GraphPresentationItem inverterItem = presentation.Find(inverter.uuid);
            GraphPresentationItem booleanItem = presentation.Find(boolean.uuid);
            GraphPresentationItem bodyItem = presentation.Find(body.uuid);
            GraphDecoratorStack stack = presentation.FindDecoratorStack(inverter.uuid);

            Assert.That(scope.PredicateRoot, Is.SameAs(inverterItem));
            Assert.That(scope.PredicateMembers, Is.EquivalentTo(new[] { inverterItem, booleanItem }));
            Assert.That(stack, Is.Not.Null);
            Assert.That(stack.Anchor, Is.SameAs(booleanItem));
            Assert.That(stack.Badges, Is.EquivalentTo(new[] { inverterItem }));
            Assert.That(presentation.Roots.Any(item => ReferenceEquals(item, inverterItem)), Is.False);
            Assert.That(presentation.Roots.Any(item => ReferenceEquals(item, booleanItem)), Is.False);
            Assert.That(presentation.ResolveMovableRoot(inverter.uuid), Is.SameAs(presentation.Find(loop.uuid).Node));
            Assert.That(presentation.ResolveMovableRoot(boolean.uuid), Is.SameAs(presentation.Find(loop.uuid).Node));
            Assert.That(scope.PredicateBounds.Contains(new Rect(inverterItem.Position, inverterItem.Size).center), Is.True);
            Assert.That(scope.PredicateBounds.Contains(new Rect(booleanItem.Position, booleanItem.Size).center), Is.True);
            Rect stackBounds = Rect.MinMaxRect(
                Mathf.Min(inverterItem.Position.x, booleanItem.Position.x),
                Mathf.Min(inverterItem.Position.y, booleanItem.Position.y),
                Mathf.Max(inverterItem.Position.x + inverterItem.Size.x, booleanItem.Position.x + booleanItem.Size.x),
                Mathf.Max(inverterItem.Position.y + inverterItem.Size.y, booleanItem.Position.y + booleanItem.Size.y));
            AssertRect(scope.PredicateBounds, stackBounds);
            Assert.That(bodyItem.Position.y, Is.GreaterThan(scope.PredicateBounds.yMax));
            Rect completionBounds = new(scope.CompletionPosition, scope.CompletionSize);
            Rect expectedScopeBounds = Rect.MinMaxRect(
                Mathf.Min(loopItem.Position.x, scope.PredicateBounds.xMin, scope.BodyFrameBounds.xMin, scope.ReturnRailX),
                Mathf.Min(loopItem.Position.y, scope.PredicateBounds.yMin, scope.BodyFrameBounds.yMin, completionBounds.yMin),
                Mathf.Max(loopItem.Position.x + loopItem.Size.x, scope.PredicateBounds.xMax, scope.BodyFrameBounds.xMax, completionBounds.xMax, scope.ExitRailX),
                Mathf.Max(loopItem.Position.y + loopItem.Size.y, scope.PredicateBounds.yMax, scope.BodyFrameBounds.yMax, completionBounds.yMax));
            AssertRect(scope.Bounds, expectedScopeBounds);
            Assert.That(tree.GraphLayout.TryGetPosition(inverter.uuid, out Vector2 storedInverter), Is.True);
            Assert.That(storedInverter, Is.EqualTo(new Vector2(900f, 700f)));
            Assert.That(tree.GraphLayout.TryGetPosition(boolean.uuid, out Vector2 storedBoolean), Is.True);
            Assert.That(storedBoolean, Is.EqualTo(new Vector2(-600f, 420f)));
            Assert.That(tree.GraphLayout.TryGetPosition(body.uuid, out Vector2 storedBody), Is.True);
            Assert.That(storedBody, Is.EqualTo(new Vector2(-1800f, 4200f)));
            Assert.That(presentation.Relations.Single(relation => relation.Kind == GraphPresentationRelationKind.LoopCondition)
                .Target.Item, Is.SameAs(inverterItem));
            Assert.That(presentation.Relations.Single(relation => relation.Kind == GraphPresentationRelationKind.LoopExit)
                .Source.Item, Is.SameAs(inverterItem));
        }

        [Test]
        public void Presentation_LoopPredicateBuildsNestedDecoratorStack()
        {
            Loop loop = Node<Loop>("Loop");
            Always always = Node<Always>("Always");
            Inverter inverter = Node<Inverter>("Inverter");
            Aethiumian.AI.Nodes.Boolean boolean = Node<Aethiumian.AI.Nodes.Boolean>("Is Ready");
            TestNode body = Node<TestNode>("Body");
            loop.loopType = Loop.LoopType.@while;
            loop.condition = always.ToReference();
            loop.events = new[] { body.ToReference() };
            always.node = inverter.ToReference();
            inverter.node = boolean.ToReference();

            GraphPresentation presentation = GraphPresentationBuilder.Build(
                GraphTopologyBuilder.Build(Tree(loop, always, inverter, boolean, body)));
            GraphPresentationLayout.Layout(presentation);
            GraphDecoratorStack stack = presentation.FindDecoratorStack(always.uuid);

            Assert.That(stack, Is.Not.Null);
            Assert.That(stack.Anchor, Is.SameAs(presentation.Find(boolean.uuid)));
            Assert.That(stack.Badges, Is.EquivalentTo(new[]
            {
                presentation.Find(always.uuid),
                presentation.Find(inverter.uuid),
            }));
            AssertRect(
                presentation.Find(loop.uuid).LoopScope.PredicateBounds,
                GetCardBounds(presentation.Find(always.uuid), presentation.Find(inverter.uuid), presentation.Find(boolean.uuid)));
        }

        [Test]
        public void Presentation_DoWhileLoopStartsWithBodyBeforeCondition()
        {
            Loop loop = Node<Loop>("Loop");
            TestNode condition = Node<TestNode>("Condition");
            TestNode body = Node<TestNode>("Body");
            loop.loopType = Loop.LoopType.doWhile;
            loop.condition = condition.ToReference();
            loop.events = new[] { body.ToReference() };
            BehaviourTreeData tree = Tree(loop, condition, body);

            GraphPresentation presentation = GraphPresentationBuilder.Build(GraphTopologyBuilder.Build(tree));
            GraphPresentationItem loopItem = presentation.Find(loop.uuid);
            GraphPresentationRelation bodyStart = presentation.Relations.Single(relation =>
                relation.Kind == GraphPresentationRelationKind.LoopBody);
            GraphPresentationRelation conditionRelation = presentation.Relations.Single(relation =>
                relation.Kind == GraphPresentationRelationKind.LoopCondition);
            GraphPresentationRelation repeatBack = presentation.Relations.Single(relation =>
                relation.Kind == GraphPresentationRelationKind.LoopRepeat
                && relation.Target.Item?.Node?.Node == body);

            Assert.That(bodyStart.Source, Is.EqualTo(loopItem.Output));
            Assert.That(bodyStart.Target.Item.Node.Node, Is.SameAs(body));
            Assert.That(conditionRelation.Source.Item.Node.Node, Is.SameAs(body));
            Assert.That(conditionRelation.Target.Item.Node.Node, Is.SameAs(condition));
            Assert.That(repeatBack.Role, Is.EqualTo(GraphPresentationRelationRole.DerivedControl));
            Assert.That(presentation.Relations.Single(relation =>
                relation.Kind == GraphPresentationRelationKind.LoopExit).Source.Item.Node.Node,
                Is.SameAs(condition));
        }

        [Test]
        public void Presentation_DoWhileLoopPlacesEmbeddedConditionAfterBody()
        {
            Loop loop = Node<Loop>("Loop");
            Inverter inverter = Node<Inverter>("Inverter");
            Aethiumian.AI.Nodes.Boolean boolean = Node<Aethiumian.AI.Nodes.Boolean>("Is Ready");
            TestNode body = Node<TestNode>("Body");
            loop.loopType = Loop.LoopType.doWhile;
            loop.condition = inverter.ToReference();
            loop.events = new[] { body.ToReference() };
            inverter.node = boolean.ToReference();
            BehaviourTreeData tree = Tree(loop, inverter, boolean, body);
            GraphTopology topology = GraphTopologyBuilder.Build(tree);
            GraphLayoutResolver.Resolve(tree, topology);

            GraphPresentation presentation = GraphPresentationBuilder.Build(topology);
            GraphPresentationLayout.Layout(presentation);

            GraphLoopScope scope = presentation.Find(loop.uuid).LoopScope;
            GraphPresentationItem bodyItem = presentation.Find(body.uuid);
            GraphDecoratorStack stack = presentation.FindDecoratorStack(inverter.uuid);

            Assert.That(bodyItem.Position.y, Is.LessThan(scope.PredicateBounds.yMin));
            Assert.That(stack, Is.Not.Null);
            Assert.That(stack.Anchor, Is.SameAs(presentation.Find(boolean.uuid)));
            AssertRect(
                scope.PredicateBounds,
                GetCardBounds(presentation.Find(inverter.uuid), presentation.Find(boolean.uuid)));
            Assert.That(presentation.Roots.Any(item => ReferenceEquals(item, presentation.Find(inverter.uuid))), Is.False);
            Assert.That(presentation.Roots.Any(item => ReferenceEquals(item, presentation.Find(boolean.uuid))), Is.False);
            Assert.That(presentation.Relations.Single(relation => relation.Kind == GraphPresentationRelationKind.LoopRepeat)
                .Source.Item, Is.SameAs(presentation.Find(inverter.uuid)));
        }

        [Test]
        public void Presentation_ForLoopUsesDerivedCountCheck()
        {
            Loop loop = Node<Loop>("Loop");
            TestNode unusedCondition = Node<TestNode>("Unused Condition");
            TestNode body = Node<TestNode>("Body");
            loop.loopType = Loop.LoopType.@for;
            loop.condition = unusedCondition.ToReference();
            loop.events = new[] { body.ToReference() };
            BehaviourTreeData tree = Tree(loop, unusedCondition, body);

            GraphPresentation presentation = GraphPresentationBuilder.Build(GraphTopologyBuilder.Build(tree));
            GraphLoopScope scope = presentation.Find(loop.uuid).LoopScope;

            Assert.That(scope.Condition.LoopJunction.Kind, Is.EqualTo(GraphLoopJunctionKind.CountCheck));
            Assert.That(scope.Condition.LoopJunction.Title, Is.EqualTo("FOR · 0"));
            Assert.That(scope.Condition.LoopJunction.Subtitle, Is.Empty);
            Assert.That(scope.Condition.LoopJunction.Tooltip, Is.EqualTo("COUNT CHECK · Uses loopCount"));
            Assert.That(presentation.Roots.Count(item => item.LoopJunction != null), Is.EqualTo(1));
            Assert.That(presentation.Relations.Any(relation =>
                relation.Kind == GraphPresentationRelationKind.LoopCondition
                && relation.Target.Item == scope.Condition
                && relation.Role == GraphPresentationRelationRole.DerivedControl), Is.True);
            GraphPresentationRelation bodyRelation = presentation.Relations.Single(relation =>
                relation.Kind == GraphPresentationRelationKind.LoopBody);
            GraphPresentationRelation repeat = presentation.Relations.Single(relation =>
                relation.Kind == GraphPresentationRelationKind.LoopRepeat);
            GraphPresentationRelation exit = presentation.Relations.Single(relation =>
                relation.Kind == GraphPresentationRelationKind.LoopExit);
            Assert.That(bodyRelation.Source, Is.EqualTo(scope.Condition.Output));
            Assert.That(repeat.Target, Is.EqualTo(scope.Condition.Entry));
            Assert.That(exit.Source, Is.EqualTo(scope.Condition.Output));
            Assert.That(exit.Target, Is.EqualTo(presentation.Find(loop.uuid).FlowComplete));
            Assert.That(presentation.Relations.Any(relation =>
                relation.Target.Item?.Node?.Node == unusedCondition), Is.False);
            Assert.That(presentation.Relations.Single(relation =>
                relation.Kind == GraphPresentationRelationKind.LoopExit).Label, Is.EqualTo("Exhausted"));
        }

        [Test]
        public void Presentation_ForLoopFormatsVariableAndMissingCountSources()
        {
            VariableData count = new("AttackCount", VariableType.Int);
            Loop variableLoop = Node<Loop>("Variable For");
            variableLoop.loopType = Loop.LoopType.@for;
            variableLoop.loopCount.SetReference(count);
            BehaviourTreeData variableTree = Tree(variableLoop);
            variableTree.variables.Add(count);
            GraphPresentation variablePresentation = GraphPresentationBuilder.Build(
                GraphTopologyBuilder.Build(variableTree));

            Assert.That(variablePresentation.Find(variableLoop.uuid).LoopScope.Condition.LoopJunction.Title,
                Is.EqualTo("FOR · $AttackCount"));

            Loop missingLoop = Node<Loop>("Missing For");
            missingLoop.loopType = Loop.LoopType.@for;
            missingLoop.loopCount.SetReference(new VariableData("RemovedCount", VariableType.Int));
            BehaviourTreeData missingTree = Tree(missingLoop);
            GraphPresentation missingPresentation = GraphPresentationBuilder.Build(
                GraphTopologyBuilder.Build(missingTree));

            Assert.That(missingPresentation.Find(missingLoop.uuid).LoopScope.Condition.LoopJunction.Title,
                Is.EqualTo("FOR · $MISSING"));
        }

        [Test]
        public void Presentation_LoopCreatesEmptyAndMissingPlaceholders()
        {
            Loop loop = Node<Loop>("Loop");
            UUID missingCondition = UUID.NewUUID();
            loop.loopType = Loop.LoopType.@while;
            loop.condition = new NodeReference(missingCondition);
            loop.events = Array.Empty<NodeReference>();
            BehaviourTreeData tree = Tree(loop);
            GraphTopology topology = GraphTopologyBuilder.Build(tree);

            GraphPresentation presentation = GraphPresentationBuilder.Build(topology);
            GraphPresentationItem[] placeholders = presentation.Roots
                .Where(item => item.LoopPlaceholder != null).ToArray();
            GraphPresentationItem condition = placeholders.Single(item =>
                item.LoopPlaceholder.Part == GraphLoopPart.Condition);
            GraphPresentationItem body = placeholders.Single(item =>
                item.LoopPlaceholder.Part == GraphLoopPart.Body);

            Assert.That(condition.LoopPlaceholder.Title, Is.EqualTo("MISSING CONDITION"));
            Assert.That(condition.LoopPlaceholder.MissingUUID, Is.EqualTo(missingCondition));
            Assert.That(body.LoopPlaceholder.Title, Is.EqualTo("EMPTY BODY"));
            Assert.That(body.LoopPlaceholder.IsMissing, Is.False);
            Assert.That(presentation.Relations.Count(relation =>
                relation.Role == GraphPresentationRelationRole.PlaceholderHint), Is.EqualTo(2));
            Assert.That(GraphLayoutResolver.CreateLayout(topology).Positions.Any(entry =>
                entry.UUID == UUID.Empty), Is.False);
        }

        [Test]
        public void Presentation_DecisionUsesDirectBranchesAndOrderedReturnSemantics()
        {
            Sequence sequence = Node<Sequence>("Sequence");
            TestNode before = Node<TestNode>("Before");
            Decision decision = Node<Decision>("Decision");
            TestNode first = Node<TestNode>("First");
            TestNode second = Node<TestNode>("Second");
            TestNode after = Node<TestNode>("After");
            sequence.events = new[] { before.ToReference(), decision.ToReference(), after.ToReference() };
            decision.events = new[] { first.ToReference(), second.ToReference() };
            BehaviourTreeData tree = Tree(sequence, before, decision, first, second, after);

            GraphPresentation presentation = GraphPresentationBuilder.Build(GraphTopologyBuilder.Build(tree));
            GraphPresentationItem owner = presentation.Find(decision.uuid);
            GraphPresentationRelation[] authored = presentation.Relations.Where(relation =>
                relation.Kind == GraphPresentationRelationKind.DecisionBranch).ToArray();
            GraphPresentationRelation[] completion = presentation.Relations.Where(relation =>
                relation.Kind == GraphPresentationRelationKind.DecisionSuccess).ToArray();
            GraphPresentationRelation failure = presentation.Relations.Single(relation =>
                relation.Kind == GraphPresentationRelationKind.DecisionFailure);
            GraphPresentationRelation continuation = presentation.Relations.Single(relation =>
                relation.Kind == GraphPresentationRelationKind.SequenceNext
                && relation.Target.Item?.Node?.Node == after);

            Assert.That(owner.DecisionScope, Is.Not.Null);
            Assert.That(authored.Select(relation => relation.Source), Is.All.EqualTo(owner.Output));
            Assert.That(authored.Select(relation => relation.Target.Item.Node.Node),
                Is.EqualTo(new TreeNode[] { first, second }));
            Assert.That(completion.Select(relation => relation.Label), Is.EqualTo(new[] { "Success", "Complete" }));
            Assert.That(completion.All(relation => relation.Target == owner.FlowComplete), Is.True);
            Assert.That(failure.Role, Is.EqualTo(GraphPresentationRelationRole.DerivedControl));
            Assert.That(failure.ContextualOwner, Is.SameAs(owner));
            Assert.That(failure.IsVisibleFor(null), Is.False);
            Assert.That(failure.IsVisibleFor(decision), Is.True);
            Assert.That(continuation.Source, Is.EqualTo(owner.FlowComplete));
        }

        [Test]
        public void Presentation_DecisionNoOptionsReturnsFailedThroughCompletion()
        {
            Decision decision = Node<Decision>("Decision");
            decision.events = Array.Empty<NodeReference>();
            BehaviourTreeData tree = Tree(decision);

            GraphPresentation presentation = GraphPresentationBuilder.Build(GraphTopologyBuilder.Build(tree));
            GraphPresentationItem owner = presentation.Find(decision.uuid);
            GraphPresentationItem placeholder = presentation.Roots.Single(item =>
                item.DecisionPlaceholder?.Kind == GraphDecisionPlaceholderKind.NoOptions);

            Assert.That(placeholder.DecisionPlaceholder.Subtitle, Is.EqualTo("Returns Failed"));
            Assert.That(presentation.Relations.Single(relation =>
                relation.Source.Item == placeholder
                && relation.Target == owner.FlowComplete).Role,
                Is.EqualTo(GraphPresentationRelationRole.DerivedCompletion));
        }

        [Test]
        public void Presentation_DecisionInvalidOptionsRemainErrorTerminals()
        {
            Decision decision = Node<Decision>("Decision");
            UUID missingUUID = UUID.NewUUID();
            decision.events = new[] { NodeReference.Empty, new NodeReference(missingUUID) };
            BehaviourTreeData tree = Tree(decision);

            GraphPresentation presentation = GraphPresentationBuilder.Build(GraphTopologyBuilder.Build(tree));
            GraphPresentationItem owner = presentation.Find(decision.uuid);
            GraphPresentationItem[] placeholders = presentation.Roots.Where(item =>
                item.DecisionPlaceholder != null).ToArray();

            Assert.That(placeholders.Select(item => item.DecisionPlaceholder.Title), Is.EqualTo(new[]
            {
                "EMPTY OPTION [0]",
                "MISSING OPTION [1]",
            }));
            Assert.That(placeholders.All(item => item.DecisionPlaceholder.Subtitle == "Returns Error"), Is.True);
            Assert.That(presentation.Relations.Any(relation =>
                relation.Target == owner.FlowComplete), Is.False);
            Assert.That(presentation.Relations.Any(relation =>
                relation.Kind == GraphPresentationRelationKind.DecisionFailure), Is.False);
            Assert.That(owner.Node.Warning, Does.Contain("Empty Decision option"));
            Assert.That(owner.Node.Warning, Does.Contain(missingUUID.ToString()));
        }

        [Test]
        public void Presentation_DecisionDuplicateTargetsKeepOccurrencesWithoutProxyCards()
        {
            Decision decision = Node<Decision>("Decision");
            TestNode target = Node<TestNode>("Target");
            decision.events = new[] { target.ToReference(), target.ToReference() };
            BehaviourTreeData tree = Tree(decision, target);

            GraphPresentation presentation = GraphPresentationBuilder.Build(GraphTopologyBuilder.Build(tree));
            GraphPresentationItem owner = presentation.Find(decision.uuid);

            Assert.That(owner.DecisionScope.Options.Count, Is.EqualTo(2));
            Assert.That(owner.DecisionScope.Options.All(option => option.Item == presentation.Find(target.uuid)), Is.True);
            Assert.That(presentation.Roots.Count(item => item.TargetUUID == target.uuid), Is.EqualTo(1));
            Assert.That(presentation.Relations.Count(relation =>
                relation.Kind == GraphPresentationRelationKind.DecisionBranch), Is.EqualTo(2));
            Assert.That(owner.Node.Warning, Does.Contain("Repeated Decision target"));
        }

        [Test]
        public void Presentation_DecisionNestedFlowReturnsFromChildCompletion()
        {
            Decision decision = Node<Decision>("Decision");
            Sequence nested = Node<Sequence>("Nested");
            TestNode nestedChild = Node<TestNode>("Nested Child");
            TestNode fallback = Node<TestNode>("Fallback");
            nested.events = new[] { nestedChild.ToReference() };
            decision.events = new[] { nested.ToReference(), fallback.ToReference() };
            BehaviourTreeData tree = Tree(decision, nested, nestedChild, fallback);

            GraphPresentation presentation = GraphPresentationBuilder.Build(GraphTopologyBuilder.Build(tree));
            GraphPresentationItem owner = presentation.Find(decision.uuid);
            GraphPresentationRelation success = presentation.Relations.Single(relation =>
                relation.Kind == GraphPresentationRelationKind.DecisionSuccess
                && relation.Label == "Success");
            GraphPresentationRelation failure = presentation.Relations.Single(relation =>
                relation.Kind == GraphPresentationRelationKind.DecisionFailure);

            Assert.That(success.Source.Item.Node.Node, Is.SameAs(nested));
            Assert.That(success.Source.Anchor, Is.EqualTo(GraphPresentationAnchorKind.FlowComplete));
            Assert.That(success.Target, Is.EqualTo(owner.FlowComplete));
            Assert.That(failure.Source.Anchor, Is.EqualTo(GraphPresentationAnchorKind.FlowComplete));
        }

        [Test]
        public void Presentation_ParallelWaitAllUsesOneCompletionPerScheduledTarget()
        {
            Parallel parallel = Node<Parallel>("Parallel");
            parallel.mode = Parallel.Mode.WaitAll;
            TestNode first = Node<TestNode>("First");
            TestNode second = Node<TestNode>("Second");
            parallel.events = new[] { first.ToReference(), second.ToReference(), first.ToReference() };
            BehaviourTreeData tree = Tree(parallel, first, second);

            GraphPresentation presentation = GraphPresentationBuilder.Build(GraphTopologyBuilder.Build(tree));
            GraphParallelScope scope = presentation.Find(parallel.uuid).ParallelScope;

            Assert.That(scope.Branches, Is.EqualTo(new[] { presentation.Find(first.uuid), presentation.Find(second.uuid) }));
            Assert.That(presentation.Relations.Count(relation => relation.Kind == GraphPresentationRelationKind.ParallelBranch), Is.EqualTo(3));
            Assert.That(presentation.Relations.Count(relation => relation.Kind == GraphPresentationRelationKind.ParallelComplete), Is.EqualTo(2));
            Assert.That(presentation.Relations.Any(relation => relation.Label == "Shared stack"), Is.True);
            Assert.That(presentation.Find(parallel.uuid).Node.HasWarning, Is.True);
        }

        [Test]
        public void Presentation_ParallelInvalidBranchesMatchWaitMode()
        {
            UUID missing = UUID.NewUUID();
            Parallel waitAll = Node<Parallel>("Wait All");
            waitAll.mode = Parallel.Mode.WaitAll;
            waitAll.events = new[] { new NodeReference(missing) };
            Parallel waitAny = Node<Parallel>("Wait Any");
            waitAny.mode = Parallel.Mode.WaitAny;
            waitAny.events = new[] { NodeReference.Empty };
            BehaviourTreeData tree = Tree(waitAll, waitAny);
            GraphPresentation presentation = GraphPresentationBuilder.Build(GraphTopologyBuilder.Build(tree));

            GraphParallelPlaceholder allPlaceholder = presentation.Find(waitAll.uuid).ParallelScope.Branches.Single().ParallelPlaceholder;
            GraphParallelPlaceholder anyPlaceholder = presentation.Find(waitAny.uuid).ParallelScope.Branches.Single().ParallelPlaceholder;

            Assert.That(allPlaceholder.Kind, Is.EqualTo(GraphParallelPlaceholderKind.IgnoredBranch));
            Assert.That(anyPlaceholder.Kind, Is.EqualTo(GraphParallelPlaceholderKind.ImmediateCompletion));
            Assert.That(presentation.Relations.Any(relation => relation.Source.Item == presentation.Find(waitAll.uuid).ParallelScope.Branches.Single()
                && relation.Kind == GraphPresentationRelationKind.ParallelComplete), Is.False);
            Assert.That(presentation.Relations.Any(relation => relation.Source.Item == presentation.Find(waitAny.uuid).ParallelScope.Branches.Single()
                && relation.Kind == GraphPresentationRelationKind.ParallelComplete), Is.True);
        }

        [Test]
        public void Presentation_ForEachMissingEnumerableReturnsFailedWithoutPersistedItems()
        {
            ForEach flow = Node<ForEach>("For Each");
            BehaviourTreeData tree = Tree(flow);
            EditorUtility.ClearDirty(tree);

            GraphPresentation presentation = GraphPresentationBuilder.Build(GraphTopologyBuilder.Build(tree));
            GraphPresentationLayout.Layout(presentation);
            GraphForEachScope scope = presentation.Find(flow.uuid).ForEachScope;

            Assert.That(scope.Check.ForEachJunction.Kind, Is.EqualTo(GraphForEachJunctionKind.EnumerableCheck));
            Assert.That(scope.Body.ForEachPlaceholder.Kind, Is.EqualTo(GraphForEachPlaceholderKind.MissingEnumerable));
            Assert.That(presentation.Relations.Any(relation => relation.Kind == GraphPresentationRelationKind.ForEachExit
                && relation.Label == "Returns Failed"), Is.True);
            Assert.That(tree.GraphLayout, Is.Null);
            Assert.That(EditorUtility.IsDirty(tree), Is.False);
        }

        [Test]
        public void Presentation_ForEachRepeatsBodyAndExitsAfterEnumeration()
        {
            ForEach flow = Node<ForEach>("For Each");
            TestNode body = Node<TestNode>("Body");
            VariableData enumerable = new("Items", VariableType.Generic);
            flow.enumerable = new VariableReference();
            flow.enumerable.SetReference(enumerable);
            flow.@event = body.ToReference();
            BehaviourTreeData tree = Tree(flow, body);
            tree.variables.Add(enumerable);

            GraphTopology topology = GraphTopologyBuilder.Build(tree);
            GraphLayoutResolver.ApplyAutoLayout(tree, topology);
            GraphPresentation presentation = GraphPresentationBuilder.Build(topology);
            GraphPresentationLayout.Layout(presentation);
            GraphForEachScope scope = presentation.Find(flow.uuid).ForEachScope;

            Assert.That(scope.Body.Node.Node, Is.SameAs(body));
            Assert.That(presentation.Relations.Any(relation => relation.Kind == GraphPresentationRelationKind.ForEachRepeat
                && relation.Source.Item.Node.Node == body && relation.Target.Item == scope.Check), Is.True);
            Assert.That(presentation.Relations.Any(relation => relation.Kind == GraphPresentationRelationKind.ForEachExit
                && relation.Target == presentation.Find(flow.uuid).FlowComplete), Is.True);
            Assert.That(scope.CompletionPosition.y, Is.GreaterThan(scope.BodyFrameBounds.yMax));
            Assert.That(GraphLayoutResolver.FindPresentationOverlaps(presentation), Is.Empty);
        }

        [Test]
        public void Presentation_SequenceShowsShortCircuitAndEmptySuccess()
        {
            Sequence sequence = Node<Sequence>("Sequence");
            TestNode first = Node<TestNode>("First");
            TestNode second = Node<TestNode>("Second");
            sequence.events = new[] { first.ToReference(), second.ToReference() };
            GraphPresentation presentation = GraphPresentationBuilder.Build(GraphTopologyBuilder.Build(Tree(sequence, first, second)));
            GraphPresentationItem owner = presentation.Find(sequence.uuid);

            Assert.That(presentation.Relations.Single(relation =>
                relation.Kind == GraphPresentationRelationKind.SequenceNext).Label, Is.EqualTo("Next"));
            GraphPresentationRelation[] failures = presentation.Relations.Where(relation =>
                relation.Kind == GraphPresentationRelationKind.SequenceFailure
                && relation.Target == owner.FlowComplete).ToArray();
            Assert.That(failures, Has.Length.EqualTo(2));
            Assert.That(failures[0].IsVisibleFor(null), Is.False);
            Assert.That(failures[0].IsVisibleFor(sequence), Is.False);
            Assert.That(failures[0].IsVisibleFor(first), Is.True);
            Assert.That(failures[0].IsVisibleFor(second), Is.False);
            Assert.That(failures[1].IsVisibleFor(second), Is.True);
            Assert.That(presentation.Relations.Single(relation =>
                relation.Kind == GraphPresentationRelationKind.SequenceSuccess).Label, Is.EqualTo("Complete"));

            Sequence empty = Node<Sequence>("Empty");
            GraphPresentation emptyPresentation = GraphPresentationBuilder.Build(GraphTopologyBuilder.Build(Tree(empty)));
            Assert.That(emptyPresentation.Relations.Single(relation =>
                relation.Kind == GraphPresentationRelationKind.SequenceSuccess).Label, Is.EqualTo("Returns Success"));
        }

        [TestCase(Aggregate.ResultMode.All, "All", "Returns Success")]
        [TestCase(Aggregate.ResultMode.Any, "Any", "Returns Failed")]
        [TestCase(Aggregate.ResultMode.True, "Returns True", "Returns True")]
        [TestCase(Aggregate.ResultMode.False, "Returns False", "Returns False")]
        public void Presentation_AggregateRunsAnUnconditionalOrderedChain(
            Aggregate.ResultMode mode,
            string completionLabel,
            string emptyLabel)
        {
            Aggregate aggregate = Node<Aggregate>("Aggregate");
            TestNode first = Node<TestNode>("First");
            TestNode second = Node<TestNode>("Second");
            aggregate.resultMode = mode;
            aggregate.events = new[] { first.ToReference(), second.ToReference() };
            GraphPresentation presentation = GraphPresentationBuilder.Build(GraphTopologyBuilder.Build(Tree(aggregate, first, second)));
            GraphPresentationItem owner = presentation.Find(aggregate.uuid);

            Assert.That(owner.AggregateScope, Is.Not.Null);
            Assert.That(owner.AggregateScope.ResultMode, Is.EqualTo(mode));
            Assert.That(presentation.Relations.Single(relation =>
                relation.Kind == GraphPresentationRelationKind.AggregateNext).Label, Is.EqualTo("Next"));
            Assert.That(presentation.Relations.Any(relation =>
                relation.Kind == GraphPresentationRelationKind.SequenceFailure), Is.False);
            Assert.That(presentation.Relations.Single(relation =>
                relation.Kind == GraphPresentationRelationKind.AggregateComplete).Label, Is.EqualTo(completionLabel));

            Aggregate empty = Node<Aggregate>("Empty Aggregate");
            empty.resultMode = mode;
            GraphPresentation emptyPresentation = GraphPresentationBuilder.Build(GraphTopologyBuilder.Build(Tree(empty)));
            Assert.That(emptyPresentation.Relations.Single(relation =>
                relation.Kind == GraphPresentationRelationKind.AggregateComplete).Label, Is.EqualTo(emptyLabel));
        }

        [Test]
        public void Presentation_NestedDecoratorStackHasOneReturnAndLevelGapPerWrapper()
        {
            Sequence sequence = Node<Sequence>("Sequence");
            Always outer = Node<Always>("Outer");
            Inverter middle = Node<Inverter>("Middle");
            ResultChanged inner = Node<ResultChanged>("Inner");
            TestNode child = Node<TestNode>("Child");
            TestNode next = Node<TestNode>("Next");
            sequence.events = new[] { outer.ToReference(), next.ToReference() };
            outer.node = middle.ToReference();
            middle.node = inner.ToReference();
            inner.node = child.ToReference();

            BehaviourTreeData tree = Tree(sequence, outer, middle, inner, child, next);
            GraphTopology topology = GraphTopologyBuilder.Build(tree);
            GraphLayoutResolver.Resolve(tree, topology);
            GraphPresentation presentation = GraphPresentationBuilder.Build(topology);
            GraphPresentationLayout.Layout(presentation);

            GraphDecoratorStack stack = presentation.FindDecoratorStack(outer.uuid);
            GraphPresentationItem childItem = presentation.Find(child.uuid);
            GraphPresentationItem nextItem = presentation.Find(next.uuid);
            Assert.That(stack, Is.Not.Null);
            Assert.That(stack.Badges.Count, Is.EqualTo(3));
            GraphPresentationRelation nextRelation = presentation.Relations.Single(relation =>
                relation.Kind == GraphPresentationRelationKind.SequenceNext
                && relation.Target.Item == nextItem);
            Assert.That(presentation.ResolveContinuationSource(nextRelation), Is.EqualTo(childItem.Completion));
            for (int index = 0; index < stack.Badges.Count; index++)
            {
                GraphPresentationItem badge = stack.Badges[index];
                GraphPresentationItem below = index + 1 < stack.Badges.Count
                    ? stack.Badges[index + 1]
                    : childItem;
                Assert.That(new Rect(badge.Position, badge.Size).yMax,
                    Is.EqualTo(new Rect(below.Position, below.Size).yMin).Within(0.01f));
            }

            GraphPresentationLayout.Layout(presentation);
            for (int index = 0; index < stack.Badges.Count; index++)
            {
                GraphPresentationItem badge = stack.Badges[index];
                GraphPresentationItem below = index + 1 < stack.Badges.Count
                    ? stack.Badges[index + 1]
                    : childItem;
                Assert.That(new Rect(badge.Position, badge.Size).yMax,
                    Is.EqualTo(new Rect(below.Position, below.Size).yMin).Within(0.01f));
            }

            Assert.That(GraphLayoutResolver.FindPresentationOverlaps(presentation), Is.Empty);
        }

        /// <summary>Asserts that two derived presentation rectangles match within layout precision.</summary>
        private static void AssertRect(Rect actual, Rect expected)
        {
            Assert.That(actual.xMin, Is.EqualTo(expected.xMin).Within(0.01f));
            Assert.That(actual.yMin, Is.EqualTo(expected.yMin).Within(0.01f));
            Assert.That(actual.xMax, Is.EqualTo(expected.xMax).Within(0.01f));
            Assert.That(actual.yMax, Is.EqualTo(expected.yMax).Within(0.01f));
        }

        /// <summary>Calculates the union of final presentation card rectangles.</summary>
        private static Rect GetCardBounds(params GraphPresentationItem[] items)
        {
            Rect bounds = new(items[0].Position, items[0].Size);
            for (int index = 1; index < items.Length; index++)
            {
                Rect itemBounds = new(items[index].Position, items[index].Size);
                bounds = Rect.MinMaxRect(
                    Mathf.Min(bounds.xMin, itemBounds.xMin),
                    Mathf.Min(bounds.yMin, itemBounds.yMin),
                    Mathf.Max(bounds.xMax, itemBounds.xMax),
                    Mathf.Max(bounds.yMax, itemBounds.yMax));
            }

            return bounds;
        }
    }
}

