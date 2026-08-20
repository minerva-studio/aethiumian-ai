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
using RepeatDecorator = Aethiumian.AI.Nodes.Repeat;

namespace Aethiumian.AI.Editor.Tests.Graph
{
    /// <summary>
    /// EditMode coverage for graph topology and non-dirty layout resolution.
    /// </summary>
    /// <summary>Graph Editor GraphPresentation contract tests.</summary>
    [Category("GraphEditor")]
    public sealed class GraphPresentationTests : GraphEditorTestFixture
    {
        [Test]
        public void ServiceOwnersHideServicePortsAndRejectMutationsWithoutBreakingExistingRelations()
        {
            TestService serviceOwner = Node<TestService>("Service owner");
            TestService existingService = Node<TestService>("Existing service");
            TestService replacementService = Node<TestService>("Replacement service");
            TestHost routineHost = Node<TestHost>("Routine host");
            TestService routineService = Node<TestService>("Routine service");
            serviceOwner.services = new List<NodeReference> { existingService.ToReference() };
            serviceOwner.child = routineHost.ToReference();
            routineHost.parent = serviceOwner.ToReference();
            BehaviourTreeData tree = Tree(serviceOwner, existingService, replacementService, routineHost, routineService);
            tree.Relink();

            GraphTopology topology = GraphTopologyBuilder.Build(tree);
            GraphPresentation presentation = GraphPresentationBuilder.Build(topology);
            IReadOnlyList<GraphPortDescriptor> ports = GraphPortDescriptorBuilder.Build(topology, presentation, includeRawReferences: false);
            Assert.That(ports.Any(port => port.OwnerUUID == serviceOwner.uuid
                && port.FieldName == nameof(ServiceHostNode.services)), Is.False);
            Assert.That(ports.Any(port => port.OwnerUUID == routineHost.uuid
                && port.FieldName == nameof(ServiceHostNode.services)), Is.True);
            Assert.That(presentation.Relations.Any(relation => relation.Kind == GraphPresentationRelationKind.Service
                && relation.Source.Item?.TargetUUID == serviceOwner.uuid
                && relation.Target.Item?.TargetUUID == existingService.uuid), Is.True);

            EditorUtility.ClearDirty(tree);
            Assert.That(tree.TryInsertReference(serviceOwner.uuid, nameof(ServiceHostNode.services), 0, replacementService.uuid, false, "Insert Service"), Is.False);
            Assert.That(tree.TryReplaceReference(serviceOwner.uuid, nameof(ServiceHostNode.services), 0, replacementService.uuid, "Replace Service"), Is.False);
            Assert.That(serviceOwner.services.Select(reference => reference.UUID), Is.EqualTo(new[] { existingService.uuid }));
            Assert.That(EditorUtility.IsDirty(tree), Is.False);

            Assert.That(tree.TryInsertReference(routineHost.uuid, nameof(ServiceHostNode.services), 0, routineService.uuid, false, "Insert Service"), Is.True);
            Assert.That(tree.TryDisconnectReference(serviceOwner.uuid, nameof(ServiceHostNode.services), 0, "Disconnect Service"), Is.True);
            Assert.That(serviceOwner.services, Is.Empty);
        }

        [Test]
        public void Presentation_BoundariesRepresentHeadAndCompletion()
        {
            Sequence head = Node<Sequence>("Head");
            Constant child = Node<Constant>("Child");
            head.events = new[] { child.ToReference() };
            child.parent = head.ToReference();
            BehaviourTreeData tree = Tree(head, child);
            GraphTopology topology = GraphTopologyBuilder.Build(tree);
            GraphPresentation presentation = GraphPresentationBuilder.Build(topology);
            GraphPresentationLayout.Layout(presentation);

            GraphPresentationItem headItem = presentation.Find(head.uuid);
            GraphPresentationRelation entrance = presentation.Relations.Single(relation =>
                relation.Kind == GraphPresentationRelationKind.Entrance);
            GraphPresentationRelation exit = presentation.Relations.Single(relation =>
                relation.Kind == GraphPresentationRelationKind.Exit);

            Assert.That(presentation.Entrance.Kind, Is.EqualTo(GraphPresentationKind.Entrance));
            Assert.That(presentation.Exit.Kind, Is.EqualTo(GraphPresentationKind.Exit));
            Assert.That(entrance.Role, Is.EqualTo(GraphPresentationRelationRole.AuthoredTreeHead));
            Assert.That(entrance.Origin, Is.Null);
            Assert.That(entrance.Source, Is.EqualTo(presentation.Entrance.Output));
            Assert.That(entrance.Target, Is.EqualTo(headItem.Entry));
            Assert.That(exit.Role, Is.EqualTo(GraphPresentationRelationRole.DerivedCompletion));
            Assert.That(exit.Source, Is.EqualTo(headItem.FlowComplete));
            Assert.That(exit.Target, Is.EqualTo(presentation.Exit.Entry));
            Assert.That(GraphPortDescriptorBuilder.Build(topology, presentation, includeRawReferences: false)
                .All(port => port.Source.Item != presentation.Entrance), Is.True);
            Assert.That(presentation.Entrance.Position, Is.EqualTo(new Vector2(
                headItem.Position.x + (headItem.Size.x - presentation.Entrance.Size.x) * 0.5f,
                headItem.Position.y - presentation.Entrance.Size.y + 1f)));

            Vector2 moveDelta = new(19f, 31f);
            headItem.Position += moveDelta;
            GraphPresentationLayout.Layout(presentation);
            Assert.That(presentation.Entrance.Position, Is.EqualTo(new Vector2(
                headItem.Position.x + (headItem.Size.x - presentation.Entrance.Size.x) * 0.5f,
                headItem.Position.y - presentation.Entrance.Size.y + 1f)));
        }

        [Test]
        public void Presentation_BoundariesRemainIsolatedWithoutHead()
        {
            TestNode node = Node<TestNode>("Detached");
            BehaviourTreeData tree = Tree(node);
            tree.headNodeUUID = UUID.Empty;
            EditorUtility.ClearDirty(tree);

            GraphPresentation presentation = GraphPresentationBuilder.Build(GraphTopologyBuilder.Build(tree));
            GraphPresentationLayout.Layout(presentation);

            Assert.That(presentation.Entrance, Is.Not.Null);
            Assert.That(presentation.Exit, Is.Not.Null);
            Assert.That(presentation.Relations.Any(relation => relation.Kind is GraphPresentationRelationKind.Entrance
                or GraphPresentationRelationKind.Exit), Is.False);
            Assert.That(presentation.Entrance.HasExplicitPosition, Is.False);
            Assert.That(presentation.Exit.HasExplicitPosition, Is.False);
            Assert.That(tree.GraphLayout, Is.Null);
            Assert.That(EditorUtility.IsDirty(tree), Is.False);
        }

        [Test]
        public void Presentation_ConditionPredicateLayoutIgnoresStoredInternalSpacing()
        {
            Condition condition = Node<Condition>("Condition");
            TestNode predicate = Node<TestNode>("Predicate");
            TestNode child = Node<TestNode>("Child");
            condition.condition = predicate.ToReference();
            predicate.child = child.ToReference();
            GraphTopology topology = GraphTopologyBuilder.Build(Tree(condition, predicate, child));
            topology.FindNode(condition.uuid).Position = new Vector2(40f, 60f);
            topology.FindNode(predicate.uuid).Position = new Vector2(1200f, 800f);
            topology.FindNode(child.uuid).Position = new Vector2(-900f, 2400f);
            Vector2 predicateStored = topology.FindNode(predicate.uuid).Position;
            Vector2 childStored = topology.FindNode(child.uuid).Position;

            GraphPresentation presentation = GraphPresentationBuilder.Build(topology);
            GraphPresentationLayout.Layout(presentation);

            GraphPresentationItem owner = presentation.Find(condition.uuid);
            GraphPresentationItem predicateItem = presentation.Find(predicate.uuid);
            GraphPresentationItem childItem = presentation.Find(child.uuid);
            Assert.That(topology.FindNode(predicate.uuid).Position, Is.EqualTo(predicateStored));
            Assert.That(topology.FindNode(child.uuid).Position, Is.EqualTo(childStored));
            Assert.That(predicateItem.Position.y, Is.LessThan(childItem.Position.y));
            Assert.That(owner.Size.y, Is.LessThan(250f));
            Assert.That(new Rect(owner.Position, owner.Size).Contains(new Rect(predicateItem.Position, predicateItem.Size).center), Is.True);
            Assert.That(new Rect(owner.Position, owner.Size).Contains(new Rect(childItem.Position, childItem.Size).center), Is.True);
        }

        [Test]
        public void Presentation_ConditionCompactsAttachedDecoratorLeafPredicate()
        {
            Condition condition = Node<Condition>("Check Wandering");
            Inverter inverter = Node<Inverter>("Inverter");
            Aethiumian.AI.Nodes.Boolean boolean = Node<Aethiumian.AI.Nodes.Boolean>("Target");
            condition.condition = inverter.ToReference();
            inverter.node = boolean.ToReference();
            GraphTopology topology = GraphTopologyBuilder.Build(Tree(condition, inverter, boolean));
            topology.FindNode(condition.uuid).Position = new Vector2(100f, 120f);

            GraphPresentation presentation = GraphPresentationBuilder.Build(topology);
            GraphPresentationLayout.Layout(presentation);

            GraphPresentationItem owner = presentation.Find(condition.uuid);
            GraphPresentationItem badge = presentation.Find(inverter.uuid);
            GraphPresentationItem leaf = presentation.Find(boolean.uuid);
            Rect predicateBounds = Rect.MinMaxRect(
                Mathf.Min(badge.Position.x, leaf.Position.x),
                Mathf.Min(badge.Position.y, leaf.Position.y),
                Mathf.Max(badge.Position.x + badge.Size.x, leaf.Position.x + leaf.Size.x),
                Mathf.Max(badge.Position.y + badge.Size.y, leaf.Position.y + leaf.Size.y));

            Assert.That(owner.Size.x, Is.EqualTo(168f).Within(0.01f));
            Assert.That(owner.Size.y,
                Is.EqualTo(
                    GraphPresentationMetrics.ConditionHeader
                    + GraphPresentationMetrics.ConditionPadding * 2f
                    + GraphPresentationMetrics.DecoratorNodeSize.y
                    + GraphPresentationMetrics.BooleanNodeSize.y)
                    .Within(0.01f));
            Assert.That(badge.Size, Is.EqualTo(GraphPresentationMetrics.DecoratorNodeSize));
            Assert.That(badge.Position.y + badge.Size.y,
                Is.EqualTo(leaf.Position.y).Within(0.01f));
            Assert.That(predicateBounds.center.x, Is.EqualTo(owner.Position.x + owner.Size.x * 0.5f).Within(0.01f));
            Assert.That(predicateBounds.yMin,
                Is.EqualTo(owner.Position.y + GraphPresentationMetrics.ConditionHeader + GraphPresentationMetrics.ConditionPadding).Within(0.01f));
        }

        [Test]
        public void Presentation_ConditionEmbedsPredicateSubtreeButLeavesBranchesExternal()
        {
            Condition condition = Node<Condition>("Condition");
            TestNode predicate = Node<TestNode>("Predicate");
            TestNode predicateChild = Node<TestNode>("Predicate Child");
            TestNode whenTrue = Node<TestNode>("True");
            condition.condition = predicate.ToReference();
            condition.trueNode = whenTrue.ToReference();
            predicate.child = predicateChild.ToReference();
            BehaviourTreeData tree = Tree(condition, predicate, predicateChild, whenTrue);
            GraphTopology topology = GraphTopologyBuilder.Build(tree);
            Vector2 predicateStoredPosition = topology.FindNode(predicate.uuid).Position;
            Vector2 childStoredPosition = topology.FindNode(predicateChild.uuid).Position;
            GraphPresentation presentation = GraphPresentationBuilder.Build(topology);
            GraphPresentationLayout.Layout(presentation);
            GraphPresentationItem owner = presentation.Find(condition.uuid);
            GraphPresentationItem predicateItem = presentation.Find(predicate.uuid);
            GraphPresentationItem childItem = presentation.Find(predicateChild.uuid);
            GraphConditionScope scope = owner.ConditionScope;

            Assert.That(owner.Slots.Single().Content, Is.SameAs(predicateItem));
            Assert.That(scope.PredicateRoot, Is.SameAs(predicateItem));
            Assert.That(scope.PredicateMembers, Is.EquivalentTo(new[] { predicateItem, childItem }));
            Assert.That(scope.PredicateRoots, Is.EquivalentTo(new[] { predicateItem, childItem }));
            Assert.That(predicateItem.Parent, Is.SameAs(owner));
            Assert.That(childItem.Parent, Is.Null);
            Assert.That(presentation.Roots.Any(item => ReferenceEquals(item, predicateItem)), Is.False);
            Assert.That(presentation.Roots.Any(item => ReferenceEquals(item, childItem)), Is.False);
            Assert.That(presentation.Find(whenTrue.uuid).Parent, Is.Null);
            Assert.That(presentation.Roots.Any(item => item == presentation.Find(whenTrue.uuid)), Is.True);
            Assert.That(topology.FindNode(predicate.uuid).Position, Is.EqualTo(predicateStoredPosition));
            Assert.That(topology.FindNode(predicateChild.uuid).Position, Is.EqualTo(childStoredPosition));

            Vector2 predicatePosition = predicateItem.Position;
            Vector2 childPosition = childItem.Position;
            Vector2 delta = new(32f, 48f);
            topology.FindNode(condition.uuid).Position += delta;
            presentation.MoveRoot(condition.uuid, owner.Position + delta);
            GraphPresentationLayout.Layout(presentation);

            Assert.That(predicateItem.Position, Is.EqualTo(predicatePosition + delta));
            Assert.That(childItem.Position, Is.EqualTo(childPosition + delta));
        }

        [Test]
        public void Presentation_ConditionContainsNestedConditionAndServiceSubtrees()
        {
            Condition outer = Node<Condition>("Outer");
            TestHost predicate = Node<TestHost>("Predicate Host");
            Condition nested = Node<Condition>("Nested");
            TestNode nestedPredicate = Node<TestNode>("Nested Predicate");
            TestNode nestedTrue = Node<TestNode>("Nested True");
            TestService service = Node<TestService>("Service");
            TestNode serviceChild = Node<TestNode>("Service Child");
            TestNode outerTrue = Node<TestNode>("Outer True");
            outer.condition = predicate.ToReference();
            outer.trueNode = outerTrue.ToReference();
            predicate.children = new[] { nested.ToReference() };
            predicate.services = new List<NodeReference> { service.ToReference() };
            nested.condition = nestedPredicate.ToReference();
            nested.trueNode = nestedTrue.ToReference();
            service.child = serviceChild.ToReference();
            BehaviourTreeData tree = Tree(
                outer,
                predicate,
                nested,
                nestedPredicate,
                nestedTrue,
                service,
                serviceChild,
                outerTrue);
            GraphTopology topology = GraphTopologyBuilder.Build(tree);
            GraphPresentation presentation = GraphPresentationBuilder.Build(topology);

            GraphPresentationLayout.Layout(presentation);

            GraphPresentationItem owner = presentation.Find(outer.uuid);
            GraphConditionScope scope = owner.ConditionScope;
            UUID[] expectedMembers =
            {
                predicate.uuid,
                nested.uuid,
                service.uuid,
                serviceChild.uuid,
            };
            Assert.That(scope.PredicateMembers.Select(item => item.TargetUUID), Is.EquivalentTo(expectedMembers));
            Assert.That(scope.NestedPredicateScopes, Is.EquivalentTo(new[] { presentation.Find(nested.uuid).ConditionScope }));
            Assert.That(presentation.Find(nested.uuid).ConditionScope.ParentPredicateScope, Is.SameAs(scope));
            Assert.That(presentation.Find(nested.uuid).ConditionScope.PredicateMembers.Select(item => item.TargetUUID),
                Is.EquivalentTo(new[] { nestedPredicate.uuid }));
            Assert.That(scope.PredicateRoots.Any(item => ReferenceEquals(item, presentation.Find(predicate.uuid))), Is.True);
            Assert.That(scope.PredicateRoots.Any(item => ReferenceEquals(item, presentation.Find(nestedPredicate.uuid))), Is.False);
            Assert.That(presentation.Find(nestedPredicate.uuid).Parent, Is.SameAs(presentation.Find(nested.uuid)));
            Assert.That(presentation.Roots.Any(item => expectedMembers.Append(nestedPredicate.uuid).Contains(item.TargetUUID)), Is.False);
            Assert.That(presentation.Roots.Any(item => item.TargetUUID == nestedTrue.uuid), Is.True);
            Assert.That(presentation.Roots.Any(item => ReferenceEquals(item, presentation.Find(outerTrue.uuid))), Is.True);

            Rect ownerBounds = new(owner.Position, owner.Size);
            foreach (GraphPresentationItem member in scope.PredicateMembers)
            {
                Rect memberBounds = new(member.Position, member.Size);
                Assert.That(ownerBounds.Overlaps(memberBounds), Is.True, member.Node.DisplayName);
            }
        }

        [Test]
        public void Presentation_DirectNestedConditionOwnsItsPredicateWithoutFlatteningScopes()
        {
            Condition outer = Node<Condition>("Outer");
            Condition nested = Node<Condition>("Nested");
            Equals nestedPredicate = Node<Equals>("Equals");
            TestNode outerTrue = Node<TestNode>("Outer True");
            TestNode outerFalse = Node<TestNode>("Outer False");
            outer.condition = nested.ToReference();
            outer.trueNode = outerTrue.ToReference();
            outer.falseNode = outerFalse.ToReference();
            nested.condition = nestedPredicate.ToReference();
            BehaviourTreeData tree = Tree(outer, nested, nestedPredicate, outerTrue, outerFalse);

            GraphTopology topology = GraphTopologyBuilder.Build(tree);
            topology.FindNode(outer.uuid).Position = new Vector2(800f, 120f);
            topology.FindNode(nested.uuid).Position = Vector2.zero;
            topology.FindNode(nestedPredicate.uuid).Position = Vector2.zero;
            GraphPresentation presentation = GraphPresentationBuilder.Build(topology);
            GraphPresentationLayout.Layout(presentation);

            GraphPresentationItem outerItem = presentation.Find(outer.uuid);
            GraphPresentationItem nestedItem = presentation.Find(nested.uuid);
            GraphPresentationItem predicateItem = presentation.Find(nestedPredicate.uuid);

            Assert.That(outerItem.ConditionScope.PredicateRoot, Is.SameAs(nestedItem));
            Assert.That(outerItem.ConditionScope.PredicateMembers, Is.EquivalentTo(new[] { nestedItem }));
            Assert.That(outerItem.ConditionScope.NestedPredicateScopes, Is.EquivalentTo(new[] { nestedItem.ConditionScope }));
            Assert.That(nestedItem.ConditionScope.ParentPredicateScope, Is.SameAs(outerItem.ConditionScope));
            Assert.That(nestedItem.ConditionScope.PredicateRoot, Is.SameAs(predicateItem));
            Assert.That(nestedItem.ConditionScope.PredicateMembers, Is.EquivalentTo(new[] { predicateItem }));
            Assert.That(nestedItem.Parent, Is.SameAs(outerItem));
            Assert.That(predicateItem.Parent, Is.SameAs(nestedItem));
            Assert.That(presentation.Roots.Contains(nestedItem), Is.False);
            Assert.That(presentation.Roots.Contains(predicateItem), Is.False);

            Rect outerBounds = new(outerItem.Position, outerItem.Size);
            Rect nestedBounds = new(nestedItem.Position, nestedItem.Size);
            Rect predicateBounds = new(predicateItem.Position, predicateItem.Size);
            Rect nestedTrueBounds = new(nestedItem.ConditionScope.TrueBranch.Position, nestedItem.ConditionScope.TrueBranch.Size);
            Rect nestedFalseBounds = new(nestedItem.ConditionScope.FalseBranch.Position, nestedItem.ConditionScope.FalseBranch.Size);
            Rect nestedCompletionBounds = new(
                nestedItem.ConditionScope.CompletionPosition,
                nestedItem.ConditionScope.CompletionSize);
            Rect nestedScopeBounds = nestedItem.ConditionScope.Bounds;
            Assert.That(outerBounds.Contains(nestedBounds.min) && outerBounds.Contains(nestedBounds.max), Is.True);
            Assert.That(nestedBounds.Contains(predicateBounds.min) && nestedBounds.Contains(predicateBounds.max), Is.True);
            Assert.That(nestedTrueBounds.yMin, Is.GreaterThan(nestedBounds.yMax));
            Assert.That(nestedFalseBounds.yMin, Is.GreaterThan(nestedBounds.yMax));
            Assert.That(nestedCompletionBounds.yMin, Is.GreaterThan(nestedBounds.yMax));
            Assert.That(outerBounds.Contains(nestedScopeBounds.min) && outerBounds.Contains(nestedScopeBounds.max), Is.True);
            Assert.That(nestedBounds.width, Is.LessThan(300f));
            Assert.That(nestedScopeBounds.xMin - outerBounds.xMin,
                Is.GreaterThanOrEqualTo(GraphPresentationMetrics.ConditionNestedScopePadding));
            Assert.That(outerBounds.xMax - nestedScopeBounds.xMax,
                Is.GreaterThanOrEqualTo(GraphPresentationMetrics.ConditionNestedScopePadding));
            Assert.That(outerBounds.yMax - nestedScopeBounds.yMax,
                Is.GreaterThanOrEqualTo(GraphPresentationMetrics.ConditionNestedScopePadding));
        }

        [Test]
        public void Presentation_ConditionPredicateCycleUsesReferenceProxyWithoutParentCycle()
        {
            Condition first = Node<Condition>("First");
            Condition second = Node<Condition>("Second");
            first.condition = second.ToReference();
            second.condition = first.ToReference();

            GraphPresentation presentation = GraphPresentationBuilder.Build(
                GraphTopologyBuilder.Build(Tree(first, second)));
            GraphPresentationLayout.Layout(presentation);

            GraphPresentationItem firstItem = presentation.Find(first.uuid);
            GraphPresentationItem secondItem = presentation.Find(second.uuid);
            GraphPresentationItem proxy = secondItem.Slots.Single().Content;
            Assert.That(firstItem.Slots.Single().Content, Is.SameAs(secondItem));
            Assert.That(proxy.Kind, Is.EqualTo(GraphPresentationKind.ReferenceProxy));
            Assert.That(proxy.TargetUUID, Is.EqualTo(first.uuid));
            Assert.That(secondItem.Parent, Is.SameAs(firstItem));
            Assert.That(firstItem.Parent, Is.Null);
            Assert.That(secondItem.ConditionScope.PredicateMembers, Is.EquivalentTo(new[] { proxy }));
            Assert.That(secondItem.Warning, Does.Contain("Predicate cycle"));
        }

        [Test]
        public void Presentation_SharedConditionPredicateUsesOneOwnerAndOneReferenceProxy()
        {
            Condition first = Node<Condition>("First");
            Condition second = Node<Condition>("Second");
            Equals shared = Node<Equals>("Shared");
            first.condition = shared.ToReference();
            second.condition = shared.ToReference();

            GraphPresentation presentation = GraphPresentationBuilder.Build(
                GraphTopologyBuilder.Build(Tree(first, shared, second)));

            GraphPresentationItem firstContent = presentation.Find(first.uuid).Slots.Single().Content;
            GraphPresentationItem secondContent = presentation.Find(second.uuid).Slots.Single().Content;
            Assert.That(firstContent, Is.SameAs(presentation.Find(shared.uuid)));
            Assert.That(secondContent.Kind, Is.EqualTo(GraphPresentationKind.ReferenceProxy));
            Assert.That(secondContent.TargetUUID, Is.EqualTo(shared.uuid));
            Assert.That(presentation.Find(shared.uuid).Parent, Is.SameAs(presentation.Find(first.uuid)));
            Assert.That(presentation.Find(second.uuid).Warning, Does.Contain("owned by another Condition"));
        }

        [Test]
        public void Presentation_DecoratorsAndLeavesUseDedicatedFootprintsAndKeepOnlyDecoratorPorts()
        {
            Always always = Node<Always>("Always");
            Inverter inverter = Node<Inverter>("Inverter");
            Capture capture = Node<Capture>("Capture");
            ResultChanged resultChanged = Node<ResultChanged>("Result Changed");
            Aethiumian.AI.Nodes.Boolean boolean = Node<Aethiumian.AI.Nodes.Boolean>("Boolean");
            Constant constant = Node<Constant>("Constant");
            TestNode child = Node<TestNode>("Child");
            always.node = child.ToReference();
            inverter.node = child.ToReference();
            capture.node = child.ToReference();
            resultChanged.node = child.ToReference();
            GraphTopology topology = GraphTopologyBuilder.Build(Tree(always, inverter, capture, resultChanged, boolean, constant, child));
            GraphPresentation presentation = GraphPresentationBuilder.Build(topology);
            IReadOnlyList<GraphPortDescriptor> ports = GraphPortDescriptorBuilder.Build(topology, presentation, includeRawReferences: false);

            Assert.That(GraphLayoutResolver.GetNodeSize(topology.FindNode(always.uuid)), Is.EqualTo(GraphPresentationMetrics.DecoratorNodeSize));
            Assert.That(GraphLayoutResolver.GetNodeSize(topology.FindNode(inverter.uuid)), Is.EqualTo(GraphPresentationMetrics.DecoratorNodeSize));
            Assert.That(GraphLayoutResolver.GetNodeSize(topology.FindNode(capture.uuid)), Is.EqualTo(GraphPresentationMetrics.DecoratorNodeSize));
            Assert.That(GraphLayoutResolver.GetNodeSize(topology.FindNode(resultChanged.uuid)), Is.EqualTo(GraphPresentationMetrics.DecoratorNodeSize));
            Assert.That(GraphLayoutResolver.GetNodeSize(topology.FindNode(boolean.uuid)), Is.EqualTo(GraphPresentationMetrics.BooleanNodeSize));
            Assert.That(GraphLayoutResolver.GetNodeSize(topology.FindNode(constant.uuid)), Is.EqualTo(GraphPresentationMetrics.ConstantNodeSize));
            Assert.That(ports.Any(port => port.OwnerUUID == always.uuid && port.FieldName == nameof(Always.node)), Is.True);
            Assert.That(ports.Any(port => port.OwnerUUID == inverter.uuid && port.FieldName == nameof(Inverter.node)), Is.True);
            Assert.That(ports.Any(port => port.OwnerUUID == capture.uuid && port.FieldName == nameof(Capture.node)), Is.True);
            Assert.That(ports.Any(port => port.OwnerUUID == resultChanged.uuid && port.FieldName == nameof(ResultChanged.node)), Is.True);
            Assert.That(ports.Any(port => port.OwnerUUID == boolean.uuid), Is.False);
            Assert.That(ports.Any(port => port.OwnerUUID == constant.uuid), Is.False);
        }

        [Test]
        public void Presentation_DecoratorStackUsesLongestSemanticTitleWidth()
        {
            Always outer = Node<Always>("Outer");
            Capture inner = Node<Capture>("Inner");
            TestNode child = Node<TestNode>("Child");
            VariableData result = new("Long Capture Result Variable", VariableType.Bool);
            outer.node = inner.ToReference();
            inner.parent = outer.ToReference();
            inner.node = child.ToReference();
            child.parent = inner.ToReference();
            inner.result.SetReference(result);

            BehaviourTreeData tree = Tree(outer, inner, child);
            tree.variables.Add(result);
            GraphTopology topology = GraphTopologyBuilder.Build(tree);
            GraphPresentation presentation = GraphPresentationBuilder.Build(topology);
            GraphPresentationLayout.Layout(presentation);
            GraphDecoratorStack stack = presentation.FindDecoratorStack(outer.uuid);

            Assert.That(stack, Is.Not.Null);
            Assert.That(stack.Badges.Select(badge => badge.Size.x).Distinct().Single(),
                Is.EqualTo(GraphPresentationMetrics.GetDecoratorNodeSize(topology.FindNode(inner.uuid), tree).x));
            Assert.That(stack.Badges[0].Size.x, Is.GreaterThan(GraphPresentationMetrics.DecoratorNodeSize.x));
        }

        [Test]
        public void Presentation_RepeatFormatsConstantDynamicAndMissingCounts()
        {
            RepeatDecorator fixedRepeat = Node<RepeatDecorator>("Fixed Repeat");
            fixedRepeat.repeatCount = 3;
            RepeatDecorator dynamicRepeat = Node<RepeatDecorator>("Dynamic Repeat");
            VariableData count = new("Repeat Count", VariableType.Int);
            dynamicRepeat.repeatCount.SetReference(count);
            RepeatDecorator missingRepeat = Node<RepeatDecorator>("Missing Repeat");
            missingRepeat.repeatCount.SetReference(new VariableData("Removed Count", VariableType.Int));

            BehaviourTreeData tree = Tree(fixedRepeat, dynamicRepeat, missingRepeat);
            tree.variables.Add(count);
            GraphTopology topology = GraphTopologyBuilder.Build(tree);

            Assert.That(GraphPresentationMetrics.GetDecoratorTitle(topology.FindNode(fixedRepeat.uuid), tree),
                Is.EqualTo("REPEAT × 3"));
            Assert.That(GraphPresentationMetrics.GetDecoratorTitle(topology.FindNode(dynamicRepeat.uuid), tree),
                Is.EqualTo("REPEAT × $Repeat Count"));
            Assert.That(GraphPresentationMetrics.GetDecoratorTitle(topology.FindNode(missingRepeat.uuid), tree),
                Is.EqualTo("REPEAT × $MISSING"));
        }

        [Test]
        public void Presentation_RepeatUsesDecoratorStackAroundSequenceChild()
        {
            RepeatDecorator repeat = Node<RepeatDecorator>("Repeat");
            Sequence sequence = Node<Sequence>("Sequence");
            Constant child = Node<Constant>("Child");
            sequence.events = new[] { child.ToReference() };
            repeat.node = sequence.ToReference();

            BehaviourTreeData tree = Tree(repeat, sequence, child);
            GraphTopology topology = GraphTopologyBuilder.Build(tree);
            GraphPresentation presentation = GraphPresentationBuilder.Build(topology);
            GraphPresentationLayout.Layout(presentation);

            GraphDecoratorStack stack = presentation.FindDecoratorStack(repeat.uuid);
            Assert.That(stack, Is.Not.Null);
            Assert.That(topology.FindNode(repeat.uuid).Shape, Is.EqualTo(GraphNodeShape.Normal));
            Assert.That(topology.FindNode(sequence.uuid).Shape, Is.EqualTo(GraphNodeShape.Flow));
            Assert.That(GraphPortDescriptorBuilder.Build(topology, presentation, includeRawReferences: false)
                .Any(port => port.OwnerUUID == repeat.uuid && port.FieldName == nameof(Decorator.node)), Is.True);
            Assert.That(stack.Anchor.TargetUUID, Is.EqualTo(sequence.uuid));
        }

        [Test]
        public void Presentation_CaptureWithoutResultVariableShowsConfigurationWarning()
        {
            Capture capture = Node<Capture>("Capture");
            TestNode child = Node<TestNode>("Child");
            capture.node = child.ToReference();

            GraphPresentation presentation = GraphPresentationBuilder.Build(
                GraphTopologyBuilder.Build(Tree(capture, child)));

            Assert.That(presentation.Find(capture.uuid).Warning, Does.Contain("Capture has no result variable"));
        }

        [Test]
        public void Presentation_LeafVisualsUseSemanticTitlesAndBoundedSizes()
        {
            Aethiumian.AI.Nodes.Boolean boolean = Node<Aethiumian.AI.Nodes.Boolean>("Authored Boolean Name");
            Constant whenTrue = Node<Constant>("Authored True Name");
            Constant whenFalse = Node<Constant>("Authored False Name");
            VariableData variable = new("A Very Long Boolean Variable Name", VariableType.Bool);
            boolean.boolean = new VariableReference();
            boolean.boolean.SetReference(variable);
            whenTrue.returnValue = true;
            whenFalse.returnValue = false;
            BehaviourTreeData tree = Tree(boolean, whenTrue, whenFalse);
            tree.variables.Add(variable);

            GraphPresentation presentation = GraphPresentationBuilder.Build(GraphTopologyBuilder.Build(tree));
            GraphLeafVisualDescriptor booleanVisual = presentation.Find(boolean.uuid).LeafVisual;
            GraphLeafVisualDescriptor trueVisual = presentation.Find(whenTrue.uuid).LeafVisual;
            GraphLeafVisualDescriptor falseVisual = presentation.Find(whenFalse.uuid).LeafVisual;

            Assert.That(booleanVisual.Title, Does.StartWith("$").And.EndWith("…"));
            Assert.That(booleanVisual.Tooltip, Does.Contain("Authored Boolean Name").And.Contain(variable.name));
            Assert.That(booleanVisual.Size, Is.EqualTo(GraphPresentationMetrics.BooleanNodeSize));
            Assert.That(trueVisual.Title, Is.EqualTo("TRUE"));
            Assert.That(falseVisual.Title, Is.EqualTo("FALSE"));
            Assert.That(trueVisual.Size, Is.EqualTo(GraphPresentationMetrics.ConstantNodeSize));
            Assert.That(falseVisual.Size, Is.EqualTo(GraphPresentationMetrics.ConstantNodeSize));

            GraphCanvasAppearance appearance = new();
            Assert.That(appearance.BooleanStroke, Is.Not.EqualTo(appearance.ConstantTrueStroke));
            Assert.That(appearance.ConstantTrueStroke, Is.Not.EqualTo(appearance.ConstantFalseStroke));
            Assert.That(appearance.ConstantTrueFillDark, Is.Not.EqualTo(appearance.ConstantFalseFillDark));
        }

        [Test]
        public void Presentation_MissingBooleanUsesSemanticWarningTitle()
        {
            Aethiumian.AI.Nodes.Boolean boolean = Node<Aethiumian.AI.Nodes.Boolean>("Custom Name");

            GraphPresentation presentation = GraphPresentationBuilder.Build(GraphTopologyBuilder.Build(Tree(boolean)));
            GraphLeafVisualDescriptor visual = presentation.Find(boolean.uuid).LeafVisual;

            Assert.That(visual.Title, Is.EqualTo("$MISSING"));
            Assert.That(visual.Tooltip, Does.Contain("Custom Name").And.Contain("$MISSING"));
            Assert.That(visual.Size, Is.EqualTo(GraphPresentationMetrics.BooleanNodeSize));
        }

        [Test]

        public void Presentation_DecoratorStackAttachesBadgesAboveRealChildWithoutRewritingDescriptors()
        {
            Capture outer = Node<Capture>("Outer");
            Inverter middle = Node<Inverter>("Middle");
            Always inner = Node<Always>("Inner");
            TestNode child = Node<TestNode>("Child");
            outer.node = middle.ToReference();
            middle.node = inner.ToReference();
            inner.node = child.ToReference();
            BehaviourTreeData tree = Tree(outer, middle, inner, child);
            GraphTopology topology = GraphTopologyBuilder.Build(tree);
            GraphNodeDescriptor outerNode = topology.FindNode(outer.uuid);
            GraphNodeDescriptor middleNode = topology.FindNode(middle.uuid);
            GraphNodeDescriptor innerNode = topology.FindNode(inner.uuid);
            GraphNodeDescriptor childNode = topology.FindNode(child.uuid);
            outerNode.Position = new Vector2(900f, 700f);
            middleNode.Position = new Vector2(600f, -300f);
            innerNode.Position = new Vector2(-400f, 300f);
            childNode.Position = new Vector2(120f, 240f);

            GraphPresentation presentation = GraphPresentationBuilder.Build(topology);
            GraphPresentationLayout.Layout(presentation);
            GraphDecoratorStack stack = presentation.FindDecoratorStack(outer.uuid);

            Assert.That(stack, Is.Not.Null);
            Assert.That(stack.Anchor.TargetUUID, Is.EqualTo(child.uuid));
            Assert.That(stack.Badges.Select(item => item.TargetUUID), Is.EqualTo(new[] { outer.uuid, middle.uuid, inner.uuid }));
            Assert.That(presentation.Find(inner.uuid).Position.y + presentation.Find(inner.uuid).Size.y,
                Is.EqualTo(presentation.Find(child.uuid).Position.y).Within(0.01f));
            Assert.That(presentation.Find(middle.uuid).Position.y + presentation.Find(middle.uuid).Size.y,
                Is.EqualTo(presentation.Find(inner.uuid).Position.y).Within(0.01f));
            Assert.That(presentation.Find(outer.uuid).Position.y + presentation.Find(outer.uuid).Size.y,
                Is.EqualTo(presentation.Find(middle.uuid).Position.y).Within(0.01f));
            Assert.That(outerNode.Position, Is.EqualTo(new Vector2(900f, 700f)));
            Assert.That(middleNode.Position, Is.EqualTo(new Vector2(600f, -300f)));
            Assert.That(innerNode.Position, Is.EqualTo(new Vector2(-400f, 300f)));
            Assert.That(childNode.Position, Is.EqualTo(new Vector2(120f, 240f)));
        }

        [Test]
        public void Presentation_DecoratorStackRejectsSharedStructuralChild()
        {
            Inverter inverter = Node<Inverter>("Inverter");
            TestHost otherParent = Node<TestHost>("Other Parent");
            TestNode child = Node<TestNode>("Child");
            inverter.node = child.ToReference();
            otherParent.children = new[] { child.ToReference() };

            GraphPresentation presentation = GraphPresentationBuilder.Build(
                GraphTopologyBuilder.Build(Tree(inverter, otherParent, child)));

            Assert.That(presentation.DecoratorStacks, Is.Empty);
            Assert.That(presentation.FindDecoratorStack(inverter.uuid), Is.Null);
            Assert.That(presentation.FindDecoratorStack(child.uuid), Is.Null);
        }

        [Test]
        public void Presentation_SequenceDecoratorAttachesToItsChild()
        {
            Sequence sequence = Node<Sequence>("Sequence");
            Inverter decorator = Node<Inverter>("Decorator");
            TestNode child = Node<TestNode>("Child");
            TestNode next = Node<TestNode>("Next");
            sequence.events = new[] { decorator.ToReference(), next.ToReference() };
            decorator.node = child.ToReference();

            GraphPresentation presentation = GraphPresentationBuilder.Build(
                GraphTopologyBuilder.Build(Tree(sequence, decorator, child, next)));
            GraphPresentationLayout.Layout(presentation);
            GraphDecoratorStack stack = presentation.FindDecoratorStack(decorator.uuid);

            Assert.That(stack, Is.Not.Null);
            Assert.That(stack.Anchor.TargetUUID, Is.EqualTo(child.uuid));
            Assert.That(stack.Badges.Select(item => item.TargetUUID), Is.EqualTo(new[] { decorator.uuid }));
            Assert.That(presentation.Find(decorator.uuid).Position.y + presentation.Find(decorator.uuid).Size.y,
                Is.EqualTo(presentation.Find(child.uuid).Position.y).Within(0.01f));
        }

        [Test]
        public void Presentation_NestedDecoratorSequenceLayoutIsStableOnFirstPass()
        {
            Sequence outer = Node<Sequence>("Outer");
            Inverter decorator = Node<Inverter>("Decorator");
            Sequence nested = Node<Sequence>("Nested");
            TestNode nestedChild = Node<TestNode>("Nested Child");
            TestNode continuation = Node<TestNode>("Continuation");
            outer.events = new[] { decorator.ToReference(), continuation.ToReference() };
            decorator.node = nested.ToReference();
            nested.events = new[] { nestedChild.ToReference() };
            BehaviourTreeData tree = Tree(outer, decorator, nested, nestedChild, continuation);
            GraphTopology topology = GraphTopologyBuilder.Build(tree);
            topology.FindNode(decorator.uuid).Position = new Vector2(700f, 500f);
            topology.FindNode(nested.uuid).Position = new Vector2(-500f, -300f);
            topology.FindNode(nestedChild.uuid).Position = new Vector2(1200f, 900f);
            topology.FindNode(continuation.uuid).Position = new Vector2(-900f, 1400f);

            GraphPresentation presentation = GraphPresentationBuilder.Build(topology);
            GraphPresentationLayout.Layout(presentation);
            GraphDecoratorStack stack = presentation.FindDecoratorStack(decorator.uuid);
            GraphSequenceScope outerScope = presentation.Find(outer.uuid).SequenceScope;
            Rect firstStackBounds = stack.VisualBounds;
            Rect firstOuterBounds = outerScope.Bounds;
            Vector2 firstCompletion = outerScope.CompletionPosition;

            Assert.That(firstOuterBounds.xMin, Is.LessThanOrEqualTo(firstStackBounds.xMin));
            Assert.That(firstOuterBounds.yMin, Is.LessThanOrEqualTo(firstStackBounds.yMin));
            Assert.That(firstOuterBounds.xMax, Is.GreaterThanOrEqualTo(firstStackBounds.xMax));
            Assert.That(firstOuterBounds.yMax, Is.GreaterThanOrEqualTo(firstStackBounds.yMax));
            Assert.That(firstCompletion.y, Is.GreaterThan(firstStackBounds.yMax));

            GraphPresentationLayout.Layout(presentation);
            Assert.That(stack.VisualBounds, Is.EqualTo(firstStackBounds));
            Assert.That(outerScope.Bounds, Is.EqualTo(firstOuterBounds));
            Assert.That(outerScope.CompletionPosition, Is.EqualTo(firstCompletion));
        }

        [Test]
        public void Presentation_ConditionBranchNestedDecoratorSequenceLayoutIsIdempotent()
        {
            Condition condition = Node<Condition>("Condition");
            Inverter decorator = Node<Inverter>("Branch Decorator");
            Sequence nested = Node<Sequence>("Branch Sequence");
            TestNode branchChild = Node<TestNode>("Branch Child");
            TestNode predicate = Node<TestNode>("Predicate");
            TestNode falseBranch = Node<TestNode>("False");
            condition.condition = predicate.ToReference();
            condition.trueNode = decorator.ToReference();
            condition.falseNode = falseBranch.ToReference();
            decorator.node = nested.ToReference();
            nested.events = new[] { branchChild.ToReference() };
            BehaviourTreeData tree = Tree(condition, decorator, nested, branchChild, predicate, falseBranch);
            GraphTopology topology = GraphTopologyBuilder.Build(tree);
            topology.FindNode(decorator.uuid).Position = new Vector2(900f, 650f);
            topology.FindNode(nested.uuid).Position = new Vector2(-700f, -400f);
            topology.FindNode(branchChild.uuid).Position = new Vector2(1100f, 1000f);

            GraphPresentation presentation = GraphPresentationBuilder.Build(topology);
            GraphPresentationLayout.Layout(presentation);
            GraphConditionScope scope = presentation.Find(condition.uuid).ConditionScope;
            GraphDecoratorStack stack = presentation.FindDecoratorStack(decorator.uuid);
            Rect firstScopeBounds = scope.Bounds;
            Rect firstStackBounds = stack.VisualBounds;
            Vector2 firstCompletion = scope.CompletionPosition;

            Assert.That(firstScopeBounds.xMin, Is.LessThanOrEqualTo(firstStackBounds.xMin));
            Assert.That(firstScopeBounds.yMin, Is.LessThanOrEqualTo(firstStackBounds.yMin));
            Assert.That(firstScopeBounds.xMax, Is.GreaterThanOrEqualTo(firstStackBounds.xMax));
            Assert.That(firstScopeBounds.yMax, Is.GreaterThanOrEqualTo(firstStackBounds.yMax));

            GraphPresentationLayout.Layout(presentation);
            Assert.That(scope.Bounds, Is.EqualTo(firstScopeBounds));
            Assert.That(stack.VisualBounds, Is.EqualTo(firstStackBounds));
            Assert.That(scope.CompletionPosition, Is.EqualTo(firstCompletion));
        }

        [Test]
        public void Presentation_ResultChangedAttachesToBooleanChild()
        {
            ResultChanged decorator = Node<ResultChanged>("Result Changed");
            Aethiumian.AI.Nodes.Boolean child = Node<Aethiumian.AI.Nodes.Boolean>("Boolean");
            decorator.node = child.ToReference();
            child.parent = decorator.ToReference();

            GraphPresentation presentation = GraphPresentationBuilder.Build(GraphTopologyBuilder.Build(Tree(decorator, child)));
            GraphPresentationLayout.Layout(presentation);
            GraphDecoratorStack stack = presentation.FindDecoratorStack(decorator.uuid);

            Assert.That(stack, Is.Not.Null);
            Assert.That(stack.Anchor.TargetUUID, Is.EqualTo(child.uuid));
            Assert.That(stack.Badges.Select(item => item.TargetUUID), Is.EqualTo(new[] { decorator.uuid }));
        }

        [Test]
        public void Presentation_EmptyDecoratorUsesChildPlaceholderAsStackAnchor()
        {
            Inverter decorator = Node<Inverter>("Empty Decorator");
            decorator.node = NodeReference.Empty;
            BehaviourTreeData tree = Tree(decorator);
            GraphTopology topology = GraphTopologyBuilder.Build(tree);
            topology.FindNode(decorator.uuid).Position = new Vector2(320f, 180f);

            GraphPresentation presentation = GraphPresentationBuilder.Build(topology);
            GraphPresentationLayout.Layout(presentation);
            GraphDecoratorStack stack = presentation.FindDecoratorStack(decorator.uuid);

            Assert.That(stack, Is.Not.Null);
            Assert.That(stack.Anchor.DecoratorPlaceholder, Is.Not.Null);
            Assert.That(stack.Badges[0].Position, Is.EqualTo(new Vector2(320f, 180f)));
            Assert.That(stack.Anchor.Position, Is.EqualTo(new Vector2(296f, 208f)));
            Assert.That(presentation.Roots.Contains(stack.Anchor), Is.False);
            Assert.That(stack.Badges.Single().TargetUUID, Is.EqualTo(decorator.uuid));
            Assert.That(presentation.ResolveMovableRoot(decorator.uuid), Is.SameAs(stack.Badges[0].Node));
            Assert.That(stack.Badges[0].Position.y + stack.Badges[0].Size.y,
                Is.EqualTo(stack.Anchor.Position.y).Within(0.01f));
        }

        [Test]
        public void Presentation_NestedEmptyDecoratorUsesOuterDecoratorAsFreePlacementOwner()
        {
            Always outer = Node<Always>("Outer Decorator");
            Inverter inner = Node<Inverter>("Inner Decorator");
            outer.node = inner.ToReference();
            inner.node = NodeReference.Empty;
            inner.parent = outer.ToReference();
            GraphTopology topology = GraphTopologyBuilder.Build(Tree(outer, inner));
            topology.FindNode(outer.uuid).Position = new Vector2(420f, 260f);
            topology.FindNode(inner.uuid).Position = new Vector2(-80f, 90f);

            GraphPresentation presentation = GraphPresentationBuilder.Build(topology);
            GraphPresentationLayout.Layout(presentation);
            GraphDecoratorStack stack = presentation.FindDecoratorStack(outer.uuid);

            Assert.That(stack, Is.Not.Null);
            Assert.That(stack.Anchor.DecoratorPlaceholder, Is.Not.Null);
            Assert.That(stack.Badges[0].Position, Is.EqualTo(new Vector2(420f, 260f)));
            Assert.That(stack.Badges[1].Position, Is.EqualTo(new Vector2(420f, 288f)));
            Assert.That(stack.Anchor.Position, Is.EqualTo(new Vector2(396f, 316f)));
            Assert.That(stack.Badges.Select(item => item.TargetUUID), Is.EqualTo(new[] { outer.uuid, inner.uuid }));
            Assert.That(presentation.ResolveMovableRoot(outer.uuid), Is.SameAs(stack.Badges[0].Node));
            Assert.That(presentation.ResolveMovableRoot(inner.uuid), Is.SameAs(stack.Badges[0].Node));
        }

        [Test]
        public void Presentation_ServiceOwnsOneScopeWithItsStructuralSubtree()
        {
            TestHost head = Node<TestHost>("Head");
            TestService service = Node<TestService>("Service");
            TestNode child = Node<TestNode>("Service Child");
            head.services = new List<NodeReference> { service.ToReference() };
            service.child = child.ToReference();
            GraphPresentation presentation = GraphPresentationBuilder.Build(
                GraphTopologyBuilder.Build(Tree(head, service, child)));
            GraphPresentationLayout.Layout(presentation);

            GraphServiceScope scope = presentation.ServiceScopes.Single();
            Assert.That(scope.Host.TargetUUID, Is.EqualTo(head.uuid));
            Assert.That(scope.Owner.TargetUUID, Is.EqualTo(service.uuid));
            Assert.That(scope.Members.Select(item => item.TargetUUID), Is.EquivalentTo(new[] { service.uuid, child.uuid }));
            Assert.That(scope.Bounds.Contains(scope.Owner.Position), Is.True);
            Assert.That(scope.Bounds.Contains(presentation.Find(child.uuid).Position), Is.True);
        }

        [Test]
        public void Presentation_SharedServiceUsesFirstHostScopeAndMarksAdditionalHost()
        {
            TestHost head = Node<TestHost>("Head");
            TestHost other = Node<TestHost>("Other Host");
            TestService service = Node<TestService>("Shared Service");
            head.children = new[] { other.ToReference() };
            head.services = new List<NodeReference> { service.ToReference() };
            other.services = new List<NodeReference> { service.ToReference() };
            GraphPresentation presentation = GraphPresentationBuilder.Build(
                GraphTopologyBuilder.Build(Tree(head, other, service)));

            GraphServiceScope scope = presentation.ServiceScopes.Single();
            Assert.That(scope.Host.TargetUUID, Is.EqualTo(head.uuid));
            Assert.That(scope.AdditionalHostCount, Is.EqualTo(1));
            Assert.That(presentation.Relations.Count(relation =>
                relation.Kind == GraphPresentationRelationKind.Service && relation.TargetUUID == service.uuid), Is.EqualTo(2));
        }

        [Test]
        public void Presentation_MissingServiceCreatesNonPersistentPlaceholder()
        {
            TestHost head = Node<TestHost>("Head");
            UUID missing = UUID.NewUUID();
            head.services = new List<NodeReference> { new(missing) };
            GraphPresentation presentation = GraphPresentationBuilder.Build(GraphTopologyBuilder.Build(Tree(head)));
            GraphPresentationLayout.Layout(presentation);

            GraphPresentationItem placeholder = presentation.Roots.Single(item => item.ServicePlaceholder != null);
            GraphPresentationRelation relation = presentation.Relations.Single(item => item.Kind == GraphPresentationRelationKind.Service);
            Assert.That(placeholder.TargetUUID, Is.EqualTo(missing));
            Assert.That(placeholder.IsRoot, Is.True);
            Assert.That(relation.Role, Is.EqualTo(GraphPresentationRelationRole.PlaceholderHint));
            Assert.That(presentation.ServiceScopes, Is.Empty);
        }

        [Test]
        public void Presentation_FlowCompletionWidthAdaptsAndClamps()
        {
            Vector2 shortSize = GraphPresentationMetrics.GetFlowCompletionSize("Flow");
            Vector2 longSize = GraphPresentationMetrics.GetFlowCompletionSize(new string('W', 100));
            Vector2 wideCharacterSize = GraphPresentationMetrics.GetFlowCompletionSize("循环条件节点名称");

            Assert.That(shortSize.x, Is.EqualTo(GraphPresentationMetrics.FlowCompletionMinimumWidth));
            Assert.That(longSize.x, Is.EqualTo(GraphPresentationMetrics.FlowCompletionMaximumWidth));
            Assert.That(wideCharacterSize.x, Is.GreaterThan(shortSize.x));
            Assert.That(shortSize.y, Is.EqualTo(GraphPresentationMetrics.FlowCompletionHeight));
        }

        [Test]
        public void Presentation_UsesSequenceOrderAndNestedConditionSlots()
        {
            Sequence sequence = Node<Sequence>("Sequence");
            TestNode first = Node<TestNode>("First");
            Condition condition = Node<Condition>("Condition");
            TestNode predicate = Node<TestNode>("Predicate");
            TestNode trueNode = Node<TestNode>("True");
            TestNode falseNode = Node<TestNode>("False");
            sequence.events = new[] { first.ToReference(), condition.ToReference() };
            condition.condition = predicate.ToReference();
            condition.trueNode = trueNode.ToReference();
            condition.falseNode = falseNode.ToReference();
            BehaviourTreeData tree = Tree(sequence, first, condition, predicate, trueNode, falseNode);

            GraphPresentation presentation = GraphPresentationBuilder.Build(GraphTopologyBuilder.Build(tree));
            GraphPresentationItem sequenceItem = presentation.Find(sequence.uuid);
            GraphPresentationItem conditionItem = presentation.Find(condition.uuid);

            Assert.That(sequenceItem.Kind, Is.EqualTo(GraphPresentationKind.Sequence));
            Assert.That(sequenceItem.IsContainer, Is.False);
            Assert.That(presentation.Roots.Any(item => item.TargetUUID == first.uuid), Is.True);
            Assert.That(presentation.Roots.Any(item => item.TargetUUID == condition.uuid), Is.True);
            Assert.That(conditionItem.Slots.Select(slot => slot.Label), Is.EqualTo(new[] { "Condition" }));
            Assert.That(conditionItem.Slots[0].Content.Node.Node, Is.SameAs(predicate));
            Assert.That(presentation.Relations.Any(edge => edge.Kind == GraphPresentationRelationKind.SequenceStart && edge.TargetUUID == first.uuid), Is.True);
            Assert.That(presentation.Relations.Any(edge => edge.Kind == GraphPresentationRelationKind.SequenceNext && edge.TargetUUID == condition.uuid), Is.True);
            Assert.That(presentation.Relations.Any(edge => edge.Kind == GraphPresentationRelationKind.ConditionTrue && edge.TargetUUID == trueNode.uuid), Is.True);
            Assert.That(presentation.Relations.Any(edge => edge.Kind == GraphPresentationRelationKind.ConditionFalse && edge.TargetUUID == falseNode.uuid), Is.True);
            Assert.That(presentation.Relations.Any(edge => edge.Kind == GraphPresentationRelationKind.FlowComplete), Is.True);
        }

        [Test]
        public void Presentation_NestedSequenceUsesCompletionBeforeOuterNext()
        {
            Sequence outer = Node<Sequence>("Outer");
            TestNode first = Node<TestNode>("A");
            Sequence inner = Node<Sequence>("Inner");
            TestNode innerFirst = Node<TestNode>("B");
            TestNode innerLast = Node<TestNode>("C");
            TestNode outerLast = Node<TestNode>("D");
            outer.events = new[] { first.ToReference(), inner.ToReference(), outerLast.ToReference() };
            inner.events = new[] { innerFirst.ToReference(), innerLast.ToReference() };
            BehaviourTreeData tree = Tree(outer, first, inner, innerFirst, innerLast, outerLast);

            GraphPresentation presentation = GraphPresentationBuilder.Build(GraphTopologyBuilder.Build(tree));

            GraphPresentationRelation outerNext = presentation.Relations.Single(relation =>
                relation.Kind == GraphPresentationRelationKind.SequenceNext
                && relation.Target.Item?.Node?.Node == outerLast);
            GraphPresentationRelation innerComplete = presentation.Relations.Single(relation =>
                relation.Kind == GraphPresentationRelationKind.SequenceSuccess
                && relation.Target.Item?.Node?.Node == inner);
            Assert.That(outerNext.Source.Item.Node.Node, Is.SameAs(inner));
            Assert.That(outerNext.Source.Anchor, Is.EqualTo(GraphPresentationAnchorKind.FlowComplete));
            Assert.That(innerComplete.Source.Item.Node.Node, Is.SameAs(innerLast));
            Assert.That(innerComplete.Source.Anchor, Is.EqualTo(GraphPresentationAnchorKind.Output));
            Assert.That(innerComplete.Role, Is.EqualTo(GraphPresentationRelationRole.DerivedCompletion));
            Assert.That(outerNext.Role, Is.EqualTo(GraphPresentationRelationRole.AuthoredReference));
            Assert.That(innerComplete.Origin, Is.Null);
            Assert.That(outerNext.Origin, Is.Not.Null);
            Assert.That(presentation.Relations.Any(relation =>
                relation.Source.Item?.Node?.Node == inner
                && relation.Source.Anchor == GraphPresentationAnchorKind.Output
                && relation.Target.Item?.Node?.Node == outerLast), Is.False);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void Presentation_NestedSequenceCompletionSupportsFirstMiddleAndLastPositions(int innerIndex)
        {
            Sequence outer = Node<Sequence>("Outer");
            Sequence inner = Node<Sequence>("Inner");
            TestNode before = Node<TestNode>("Before");
            TestNode innerEvent = Node<TestNode>("Inner Event");
            TestNode after = Node<TestNode>("After");
            TreeNode[] authoredEvents = { before, after };
            NodeReference[] eventReferences = authoredEvents.Select(node => node.ToReference()).ToArray();
            outer.events = eventReferences.Take(innerIndex)
                .Append(inner.ToReference())
                .Concat(eventReferences.Skip(innerIndex))
                .ToArray();
            inner.events = new[] { innerEvent.ToReference() };
            BehaviourTreeData tree = Tree(outer, before, inner, innerEvent, after);

            GraphPresentation presentation = GraphPresentationBuilder.Build(GraphTopologyBuilder.Build(tree));

            GraphPresentationRelation innerStart = presentation.Relations.Single(relation =>
                relation.Target.Item?.Node?.Node == inner
                && relation.Target.Anchor == GraphPresentationAnchorKind.Entry);
            GraphPresentationRelation innerCompletion = presentation.Relations.Single(relation =>
                relation.Kind == GraphPresentationRelationKind.SequenceSuccess
                && relation.Target.Item?.Node?.Node == inner);
            TreeNode expectedPredecessor = innerIndex == 0
                ? outer
                : authoredEvents[innerIndex - 1];
            Assert.That(innerStart.Source.Item?.Node?.Node,
                Is.SameAs(expectedPredecessor));
            Assert.That(innerCompletion.Source.Item.Node.Node, Is.SameAs(innerEvent));

            GraphPresentationRelation continuation = presentation.Relations.Single(relation =>
                relation.Kind is GraphPresentationRelationKind.SequenceNext or GraphPresentationRelationKind.SequenceSuccess
                && relation.Source.Item?.Node?.Node == inner
                && relation.Source.Anchor == GraphPresentationAnchorKind.FlowComplete);
            if (innerIndex == outer.events.Length - 1)
            {
                Assert.That(continuation.Target.Item.Node.Node, Is.SameAs(outer));
                Assert.That(continuation.Target.Anchor, Is.EqualTo(GraphPresentationAnchorKind.FlowComplete));
            }
            else
            {
                Assert.That(continuation.Target.Item.Node.Node, Is.SameAs(authoredEvents[innerIndex]));
                Assert.That(continuation.Target.Anchor, Is.EqualTo(GraphPresentationAnchorKind.Entry));
            }
        }

        [Test]
        public void Presentation_DeeplyNestedSequencesComposeCompletionEndpoints()
        {
            Sequence outer = Node<Sequence>("Outer");
            Sequence middle = Node<Sequence>("Middle");
            Sequence inner = Node<Sequence>("Inner");
            TestNode leaf = Node<TestNode>("Leaf");
            TestNode tail = Node<TestNode>("Tail");
            outer.events = new[] { middle.ToReference(), tail.ToReference() };
            middle.events = new[] { inner.ToReference() };
            inner.events = new[] { leaf.ToReference() };
            BehaviourTreeData tree = Tree(outer, middle, inner, leaf, tail);

            GraphPresentation presentation = GraphPresentationBuilder.Build(GraphTopologyBuilder.Build(tree));

            GraphPresentationRelation innerToMiddle = presentation.Relations.Single(relation =>
                relation.Kind == GraphPresentationRelationKind.SequenceSuccess
                && relation.Target.Item?.Node?.Node == middle);
            GraphPresentationRelation middleToTail = presentation.Relations.Single(relation =>
                relation.Kind == GraphPresentationRelationKind.SequenceNext
                && relation.Target.Item?.Node?.Node == tail);
            Assert.That(innerToMiddle.Source.Item.Node.Node, Is.SameAs(inner));
            Assert.That(innerToMiddle.Source.Anchor, Is.EqualTo(GraphPresentationAnchorKind.FlowComplete));
            Assert.That(middleToTail.Source.Item.Node.Node, Is.SameAs(middle));
            Assert.That(middleToTail.Source.Anchor, Is.EqualTo(GraphPresentationAnchorKind.FlowComplete));
        }

        [Test]
        public void Presentation_EmptySequenceConnectsDirectlyToCompletion()
        {
            Sequence sequence = Node<Sequence>("Empty");
            BehaviourTreeData tree = Tree(sequence);

            GraphPresentation presentation = GraphPresentationBuilder.Build(GraphTopologyBuilder.Build(tree));

            GraphPresentationRelation relation = presentation.Relations.Single(candidate =>
                candidate.Kind == GraphPresentationRelationKind.SequenceSuccess);
            Assert.That(relation.Kind, Is.EqualTo(GraphPresentationRelationKind.SequenceSuccess));
            Assert.That(relation.Source.Anchor, Is.EqualTo(GraphPresentationAnchorKind.Output));
            Assert.That(relation.Target.Anchor, Is.EqualTo(GraphPresentationAnchorKind.FlowComplete));
            Assert.That(presentation.CompletionScopes.Count, Is.EqualTo(1));
        }

        [Test]
        public void Presentation_UsesProxyForDuplicateAndMissingReferences()
        {
            Sequence sequence = Node<Sequence>("Sequence");
            TestNode child = Node<TestNode>("Child");
            UUID missing = UUID.NewUUID();
            sequence.events = new[] { child.ToReference(), child.ToReference(), new NodeReference(missing) };
            BehaviourTreeData tree = Tree(sequence, child);

            GraphPresentation presentation = GraphPresentationBuilder.Build(GraphTopologyBuilder.Build(tree));
            Assert.That(presentation.Roots.Count(item => item.TargetUUID == child.uuid), Is.EqualTo(1));
            Assert.That(presentation.Relations.Count(edge => edge.TargetUUID == child.uuid), Is.EqualTo(2));
            Assert.That(presentation.Relations.Where(edge => edge.TargetUUID == child.uuid)
                .Select(edge => edge.OccurrenceId).Distinct().Count(), Is.EqualTo(2));
            Assert.That(presentation.Relations.Any(edge => edge.Kind == GraphPresentationRelationKind.SequenceStart && edge.TargetUUID == child.uuid), Is.True);
            Assert.That(presentation.Relations.Any(edge => edge.Kind == GraphPresentationRelationKind.SequenceNext && edge.TargetUUID == child.uuid), Is.True);
            Assert.That(presentation.Relations.Any(edge => edge.IsMissingTarget), Is.True);
        }

        [Test]
        public void Presentation_ConditionConvergesBeforeOuterSequenceContinuation()
        {
            Sequence outer = Node<Sequence>("Outer");
            TestNode before = Node<TestNode>("Before");
            Condition condition = Node<Condition>("Condition");
            TestNode predicate = Node<TestNode>("Predicate");

            TestNode trueNode = Node<TestNode>("True");
            TestNode falseNode = Node<TestNode>("False");
            TestNode after = Node<TestNode>("After");
            outer.events = new[] { before.ToReference(), condition.ToReference(), after.ToReference() };
            condition.condition = predicate.ToReference();
            condition.trueNode = trueNode.ToReference();
            condition.falseNode = falseNode.ToReference();
            BehaviourTreeData tree = Tree(outer, before, condition, predicate, trueNode, falseNode, after);

            GraphPresentation presentation = GraphPresentationBuilder.Build(GraphTopologyBuilder.Build(tree));
            GraphPresentationItem conditionItem = presentation.Find(condition.uuid);
            GraphPresentationRelation continuation = presentation.Relations.Single(relation =>
                relation.Kind == GraphPresentationRelationKind.SequenceNext
                && relation.Target.Item?.Node?.Node == after);
            GraphPresentationRelation[] completions = presentation.Relations.Where(relation =>
                relation.Role == GraphPresentationRelationRole.DerivedCompletion
                && relation.Target == conditionItem.FlowComplete).ToArray();

            Assert.That(conditionItem.ConditionScope, Is.Not.Null);
            Assert.That(completions.Length, Is.EqualTo(2));
            Assert.That(completions.Select(relation => relation.Source.Item.Node.Node),
                Is.EquivalentTo(new TreeNode[] { trueNode, falseNode }));
            Assert.That(completions.All(relation => relation.Source.Anchor == GraphPresentationAnchorKind.Output), Is.True);
            Assert.That(completions.All(relation => !relation.IsEditableReference), Is.True);
            Assert.That(continuation.Source, Is.EqualTo(conditionItem.FlowComplete));
            Assert.That(continuation.Role, Is.EqualTo(GraphPresentationRelationRole.AuthoredReference));
            Assert.That(continuation.IsEditableReference, Is.True);
            Assert.That(presentation.Relations.Any(relation =>
                relation.Source == conditionItem.Output
                && relation.Target.Item?.Node?.Node == after), Is.False);
        }

        [Test]
        public void Presentation_ConditionSequenceBranchesConvergeFromSequenceEnds()
        {
            Condition condition = Node<Condition>("Condition");
            TestNode predicate = Node<TestNode>("Predicate");
            Sequence trueSequence = Node<Sequence>("True Sequence");
            Sequence falseSequence = Node<Sequence>("False Sequence");
            TestNode trueLeaf = Node<TestNode>("True Leaf");
            TestNode falseLeaf = Node<TestNode>("False Leaf");
            condition.condition = predicate.ToReference();
            condition.trueNode = trueSequence.ToReference();
            condition.falseNode = falseSequence.ToReference();
            trueSequence.events = new[] { trueLeaf.ToReference() };
            falseSequence.events = new[] { falseLeaf.ToReference() };
            BehaviourTreeData tree = Tree(condition, predicate, trueSequence, falseSequence, trueLeaf, falseLeaf);

            GraphPresentation presentation = GraphPresentationBuilder.Build(GraphTopologyBuilder.Build(tree));
            GraphPresentationItem conditionItem = presentation.Find(condition.uuid);
            GraphPresentationRelation[] branchCompletions = presentation.Relations.Where(relation =>
                relation.Role == GraphPresentationRelationRole.DerivedCompletion
                && relation.Target == conditionItem.FlowComplete).ToArray();

            Assert.That(branchCompletions.Length, Is.EqualTo(2));
            Assert.That(branchCompletions.All(relation => relation.Source.Anchor == GraphPresentationAnchorKind.FlowComplete), Is.True);
            Assert.That(branchCompletions.Select(relation => relation.Source.Item.Node.Node),
                Is.EquivalentTo(new TreeNode[] { trueSequence, falseSequence }));
        }

        [Test]
        public void Presentation_ConditionDecoratorWrappedSequenceConvergesFromWrappedCompletion()
        {
            Condition condition = Node<Condition>("Condition");
            TestNode predicate = Node<TestNode>("Predicate");
            Always decorator = Node<Always>("Always");
            Sequence trueSequence = Node<Sequence>("True Sequence");
            TestNode trueLeaf = Node<TestNode>("True Leaf");
            TestNode falseNode = Node<TestNode>("False");
            condition.condition = predicate.ToReference();
            condition.trueNode = decorator.ToReference();
            condition.falseNode = falseNode.ToReference();
            decorator.node = trueSequence.ToReference();
            trueSequence.events = new[] { trueLeaf.ToReference() };

            GraphTopology topology = GraphTopologyBuilder.Build(
                Tree(condition, predicate, decorator, trueSequence, trueLeaf, falseNode));
            GraphPresentation presentation = GraphPresentationBuilder.Build(topology);
            GraphPresentationLayout.Layout(presentation);
            IReadOnlyList<GraphPortDescriptor> ports = GraphPortDescriptorBuilder.Build(
                topology, presentation, includeRawReferences: false);
            GraphEdgeLayerElement edgeLayer = new(new GraphCanvasAppearance());
            edgeLayer.SetPresentation(presentation, ports);
            GraphPresentationItem conditionItem = presentation.Find(condition.uuid);
            GraphPresentationRelation trueCompletion = presentation.Relations.Single(relation =>
                relation.Kind == GraphPresentationRelationKind.ConditionTrue
                && relation.Target.Item?.TargetUUID == decorator.uuid);
            GraphPresentationRelation branchCompletion = presentation.Relations.Single(relation =>
                relation.Role == GraphPresentationRelationRole.DerivedCompletion
                && relation.Target == conditionItem.FlowComplete
                && relation.Source.Item?.TargetUUID == decorator.uuid);

            Assert.That(trueCompletion.Target.Item.TargetUUID, Is.EqualTo(decorator.uuid));
            Assert.That(presentation.ResolveContinuationSource(branchCompletion),
                Is.EqualTo(presentation.Find(trueSequence.uuid).FlowComplete));
            Assert.That(presentation.Find(trueSequence.uuid).Completion.Anchor,
                Is.EqualTo(GraphPresentationAnchorKind.FlowComplete));
            GraphPresentationItem sequenceItem = presentation.Find(trueSequence.uuid);
            Vector2 expected = new Vector2(
                sequenceItem.FlowScope.CompletionPosition.x + sequenceItem.FlowScope.CompletionSize.x * 0.5f,
                sequenceItem.FlowScope.CompletionPosition.y + sequenceItem.FlowScope.CompletionSize.y);
            Assert.That(edgeLayer.GetSourceAnchor(branchCompletion), Is.EqualTo(expected));
        }

        [Test]
        public void Presentation_ConditionCreatesEmptyAndMissingFallbackPlaceholders()
        {
            Condition condition = Node<Condition>("Condition");
            TestNode predicate = Node<TestNode>("Predicate");
            UUID missingUUID = UUID.NewUUID();
            condition.condition = predicate.ToReference();
            condition.trueNode = NodeReference.Empty;
            condition.falseNode = new NodeReference(missingUUID);
            BehaviourTreeData tree = Tree(condition, predicate);
            GraphTopology topology = GraphTopologyBuilder.Build(tree);

            GraphPresentation presentation = GraphPresentationBuilder.Build(topology);
            GraphPresentationItem[] placeholders = presentation.Roots.Where(item => item.Placeholder != null).ToArray();
            GraphPresentationItem empty = placeholders.Single(item => item.Placeholder.Branch == GraphConditionBranch.True);
            GraphPresentationItem missing = placeholders.Single(item => item.Placeholder.Branch == GraphConditionBranch.False);

            Assert.That(empty.Placeholder.Title, Is.EqualTo("EMPTY TRUE"));
            Assert.That(empty.Placeholder.Subtitle, Is.EqualTo("Returns Success"));
            Assert.That(empty.Placeholder.IsMissing, Is.False);
            Assert.That(missing.Placeholder.Title, Is.EqualTo("MISSING FALSE"));
            Assert.That(missing.Placeholder.Subtitle, Is.EqualTo("Returns Failed"));
            Assert.That(missing.Placeholder.MissingUUID, Is.EqualTo(missingUUID));
            Assert.That(missing.Warning, Does.Contain(missingUUID.ToString()));
            Assert.That(topology.FindNode(condition.uuid).HasWarning, Is.True);
            Assert.That(presentation.Relations.Count(relation =>
                relation.Role == GraphPresentationRelationRole.PlaceholderHint), Is.EqualTo(2));
            Assert.That(presentation.Relations.Where(relation =>
                relation.Role == GraphPresentationRelationRole.PlaceholderHint)
                .All(relation => !relation.IsEditableReference), Is.True);
            Assert.That(presentation.Relations.Count(relation =>
                relation.Role == GraphPresentationRelationRole.DerivedCompletion
                && relation.Target.Item == presentation.Find(condition.uuid)), Is.EqualTo(2));
            Assert.That(GraphLayoutResolver.CreateLayout(topology).Positions.Count, Is.EqualTo(topology.Nodes.Count));
            Assert.That(GraphLayoutResolver.CreateLayout(topology).Positions.Any(entry => entry.UUID == UUID.Empty), Is.False);
        }

        [Test]
        public void Presentation_ConditionDuplicateTargetKeepsBothBranchOccurrences()
        {
            Condition condition = Node<Condition>("Condition");
            TestNode predicate = Node<TestNode>("Predicate");
            TestNode shared = Node<TestNode>("Shared");
            condition.condition = predicate.ToReference();
            condition.trueNode = shared.ToReference();
            condition.falseNode = shared.ToReference();
            BehaviourTreeData tree = Tree(condition, predicate, shared);

            GraphPresentation presentation = GraphPresentationBuilder.Build(GraphTopologyBuilder.Build(tree));
            GraphPresentationRelation[] authored = presentation.Relations.Where(relation =>
                relation.TargetUUID == shared.uuid
                && relation.Role == GraphPresentationRelationRole.AuthoredReference).ToArray();
            GraphPresentationRelation[] derived = presentation.Relations.Where(relation =>
                relation.TargetUUID == shared.uuid
                && relation.Role == GraphPresentationRelationRole.DerivedCompletion).ToArray();

            Assert.That(authored.Length, Is.EqualTo(2));
            Assert.That(derived.Length, Is.EqualTo(2));
            Assert.That(authored.Select(relation => relation.OccurrenceId), Is.EquivalentTo(derived.Select(relation => relation.OccurrenceId)));
            Assert.That(presentation.Roots.Count(item => item.Node?.Node == shared), Is.EqualTo(1));
        }

        [Test]
        public void Presentation_MovingLoopOwnerKeepsDerivedBodyCompactWithoutLayoutWrite()
        {
            Loop loop = Node<Loop>("Loop");
            TestNode condition = Node<TestNode>("Condition");
            TestNode body = Node<TestNode>("Body");
            loop.loopType = Loop.LoopType.@while;
            loop.condition = condition.ToReference();
            loop.events = new[] { body.ToReference() };
            BehaviourTreeData tree = Tree(loop, condition, body);
            GraphPresentation presentation = GraphPresentationBuilder.Build(GraphTopologyBuilder.Build(tree));
            GraphPresentationLayout.Layout(presentation);
            GraphLoopScope scope = presentation.Find(loop.uuid).LoopScope;
            GraphPresentationItem loopItem = presentation.Find(loop.uuid);
            GraphPresentationItem bodyItem = presentation.Find(body.uuid);
            Vector2 initialOffset = bodyItem.Position - loopItem.Position;
            Vector2 movedPosition = loopItem.Position + new Vector2(240f, 120f);
            EditorUtility.ClearDirty(tree);

            Assert.That(presentation.ResolveMovableRoot(body.uuid), Is.SameAs(loopItem.Node));
            presentation.MoveRoot(loop.uuid, movedPosition);
            GraphPresentationLayout.Layout(presentation);

            Rect movedBounds = GraphPresentationLayout.GetBounds(bodyItem);
            Assert.That(bodyItem.Position - loopItem.Position, Is.EqualTo(initialOffset));
            Assert.That(scope.BodyFrameBounds.xMin, Is.LessThanOrEqualTo(movedBounds.xMin));
            Assert.That(scope.BodyFrameBounds.xMax, Is.GreaterThanOrEqualTo(movedBounds.xMax));
            Assert.That(scope.CompletionPosition.y, Is.GreaterThan(scope.BodyFrameBounds.yMax));
            Assert.That(tree.GraphLayout, Is.Null);
            Assert.That(EditorUtility.IsDirty(tree), Is.False);
        }

        [Test]
        public void Presentation_LoopBodyDecoratorAnchorIsStableAfterSingleLayout()
        {
            Loop loop = Node<Loop>("Loop");
            TestNode condition = Node<TestNode>("Condition");
            Inverter decorator = Node<Inverter>("Body Decorator");
            Sequence body = Node<Sequence>("Body Sequence");
            TestNode bodyChild = Node<TestNode>("Body Child");
            loop.loopType = Loop.LoopType.@while;
            loop.condition = condition.ToReference();
            loop.events = new[] { decorator.ToReference() };
            decorator.node = body.ToReference();
            body.events = new[] { bodyChild.ToReference() };
            BehaviourTreeData tree = Tree(loop, condition, decorator, body, bodyChild);
            GraphTopology topology = GraphTopologyBuilder.Build(tree);
            topology.FindNode(decorator.uuid).Position = new Vector2(900f, -500f);
            topology.FindNode(body.uuid).Position = new Vector2(-700f, 1200f);

            GraphPresentation presentation = GraphPresentationBuilder.Build(topology);
            GraphPresentationLayout.Layout(presentation);
            GraphLoopScope scope = presentation.Find(loop.uuid).LoopScope;
            GraphDecoratorStack stack = presentation.FindDecoratorStack(decorator.uuid);
            Rect firstBounds = scope.Bounds;
            Rect firstStackBounds = stack.VisualBounds;
            Vector2 firstCompletion = scope.CompletionPosition;

            Assert.That(firstBounds.xMin, Is.LessThanOrEqualTo(firstStackBounds.xMin));
            Assert.That(firstBounds.yMin, Is.LessThanOrEqualTo(firstStackBounds.yMin));
            Assert.That(firstBounds.xMax, Is.GreaterThanOrEqualTo(firstStackBounds.xMax));
            Assert.That(firstBounds.yMax, Is.GreaterThanOrEqualTo(firstStackBounds.yMax));
            GraphPresentationLayout.Layout(presentation);
            Assert.That(scope.Bounds, Is.EqualTo(firstBounds));
            Assert.That(stack.VisualBounds, Is.EqualTo(firstStackBounds));
            Assert.That(scope.CompletionPosition, Is.EqualTo(firstCompletion));
        }

        [Test]
        public void Presentation_MovingConditionBranchRecalculatesDerivedScopeOnly()
        {
            Condition condition = Node<Condition>("Condition");
            TestNode predicate = Node<TestNode>("Predicate");
            TestNode trueNode = Node<TestNode>("True");
            TestNode falseNode = Node<TestNode>("False");
            condition.condition = predicate.ToReference();
            condition.trueNode = trueNode.ToReference();
            condition.falseNode = falseNode.ToReference();
            BehaviourTreeData tree = Tree(condition, predicate, trueNode, falseNode);
            GraphTopology topology = GraphTopologyBuilder.Build(tree);
            GraphLayoutResolver.Resolve(tree, topology);
            GraphPresentation presentation = GraphPresentationBuilder.Build(topology);
            GraphPresentationLayout.Layout(presentation);
            GraphConditionScope scope = presentation.Find(condition.uuid).ConditionScope;
            float originalCompletionY = scope.CompletionPosition.y;
            Vector2 descriptorPosition = topology.FindNode(trueNode.uuid).Position;

            presentation.MoveRoot(trueNode.uuid, presentation.Find(trueNode.uuid).Position + Vector2.up * 400f);
            GraphPresentationLayout.Layout(presentation);

            Assert.That(scope.CompletionPosition.y, Is.GreaterThan(originalCompletionY));
            Assert.That(topology.FindNode(trueNode.uuid).Position, Is.EqualTo(descriptorPosition));
            Assert.That(tree.GraphLayout, Is.Null);
        }

        [Test]
        public void Presentation_MovingDecisionBranchRecalculatesCompletionWithoutLayoutWrite()
        {
            Decision decision = Node<Decision>("Decision");
            TestNode first = Node<TestNode>("First");
            TestNode second = Node<TestNode>("Second");
            decision.events = new[] { first.ToReference(), second.ToReference() };
            BehaviourTreeData tree = Tree(decision, first, second);
            GraphTopology topology = GraphTopologyBuilder.Build(tree);
            GraphLayoutResolver.Resolve(tree, topology);
            GraphPresentation presentation = GraphPresentationBuilder.Build(topology);
            GraphPresentationLayout.Layout(presentation);
            GraphDecisionScope scope = presentation.Find(decision.uuid).DecisionScope;
            float originalCompletionY = scope.CompletionPosition.y;
            Vector2 descriptorPosition = topology.FindNode(first.uuid).Position;

            presentation.MoveRoot(first.uuid, presentation.Find(first.uuid).Position + Vector2.up * 400f);
            GraphPresentationLayout.Layout(presentation);

            Assert.That(scope.CompletionPosition.y, Is.GreaterThan(originalCompletionY));
            Assert.That(topology.FindNode(first.uuid).Position, Is.EqualTo(descriptorPosition));
            Assert.That(tree.GraphLayout, Is.Null);
        }

        [Test]
        public void Presentation_ClassifiesDecisionProbabilityAndParallelBranches()
        {
            Decision decision = Node<Decision>("Decision");
            TestNode decisionA = Node<TestNode>("Decision A");
            TestNode decisionB = Node<TestNode>("Decision B");
            decision.events = new[] { decisionA.ToReference(), decisionB.ToReference() };

            Probability probability = Node<Probability>("Probability");
            TestNode probabilityA = Node<TestNode>("Probability A");
            probability.events = new[]
            {
                new Probability.EventWeight { reference = probabilityA.ToReference(), weight = 25 },
            };

            Parallel parallel = Node<Parallel>("Parallel");
            TestNode parallelA = Node<TestNode>("Parallel A");
            parallel.events = new[] { parallelA.ToReference() };

            BehaviourTreeData tree = Tree(decision, decisionA, decisionB, probability, probabilityA, parallel, parallelA);
            GraphPresentation presentation = GraphPresentationBuilder.Build(GraphTopologyBuilder.Build(tree));

            Assert.That(presentation.Relations.Count(edge => edge.Kind == GraphPresentationRelationKind.DecisionBranch), Is.EqualTo(2));
            Assert.That(presentation.Relations.Any(edge => edge.Kind == GraphPresentationRelationKind.ProbabilityBranch && edge.Label.Contains("Weight")), Is.True);
            Assert.That(presentation.Relations.Any(edge => edge.Kind == GraphPresentationRelationKind.ParallelBranch), Is.True);
            Assert.That(presentation.Roots.Count(item => item.IsContainer), Is.EqualTo(0));
        }

        [Test]
        public void Presentation_NodeFamiliesUseExactSemanticSizes()
        {
            TestNode normal = Node<TestNode>("Normal");
            Sequence flow = Node<Sequence>("Flow");
            Condition branch = Node<Condition>("Branch");
            Decision decision = Node<Decision>("Decision");
            TestService service = Node<TestService>("Service");
            BehaviourTreeData tree = Tree(normal, flow, branch, decision, service);
            GraphTopology topology = GraphTopologyBuilder.Build(tree);

            Assert.That(GraphLayoutResolver.GetNodeSize(topology.FindNode(normal.uuid)),
                Is.EqualTo(GraphPresentationMetrics.NormalNodeSize));
            Assert.That(GraphLayoutResolver.GetNodeSize(topology.FindNode(flow.uuid)),
                Is.EqualTo(GraphPresentationMetrics.FlowNodeSize));
            Assert.That(GraphLayoutResolver.GetNodeSize(topology.FindNode(branch.uuid)),
                Is.EqualTo(GraphPresentationMetrics.BranchNodeSize));
            Assert.That(GraphLayoutResolver.GetNodeSize(topology.FindNode(decision.uuid)),
                Is.EqualTo(new Vector2(176f, 76f)));
            Assert.That(GraphLayoutResolver.GetNodeSize(topology.FindNode(service.uuid)),
                Is.EqualTo(GraphPresentationMetrics.ServiceNodeSize));
            Assert.That(GraphPresentationMetrics.FlowNodeSize, Is.EqualTo(new Vector2(188f, 52f)));
            Assert.That(GraphPresentationMetrics.BranchNodeSize, Is.EqualTo(new Vector2(176f, 52f)));
            Assert.That(GraphPresentationMetrics.NormalNodeSize, Is.EqualTo(new Vector2(168f, 40f)));
            Assert.That(GraphPresentationMetrics.ServiceNodeSize, Is.EqualTo(new Vector2(152f, 40f)));
            Assert.That(GraphPresentationMetrics.DecoratorNodeSize, Is.EqualTo(new Vector2(112f, 28f)));
            Assert.That(GraphPresentationMetrics.BooleanNodeSize, Is.EqualTo(new Vector2(112f, 26f)));
            Assert.That(GraphPresentationMetrics.ConstantNodeSize, Is.EqualTo(new Vector2(64f, 24f)));
            Assert.That(GraphPresentationMetrics.FlowNodeSize.x, Is.GreaterThan(GraphPresentationMetrics.NormalNodeSize.x));
            Assert.That(GraphPresentationMetrics.BranchNodeSize.x, Is.GreaterThan(GraphPresentationMetrics.NormalNodeSize.x));
            Assert.That(GraphPresentationMetrics.NormalNodeSize.x, Is.GreaterThan(GraphPresentationMetrics.ServiceNodeSize.x));
            Assert.That(GraphPresentationMetrics.FlowNodeSize.y, Is.GreaterThan(GraphPresentationMetrics.NormalNodeSize.y));
            Assert.That(GraphPresentationMetrics.BranchNodeSize.y, Is.GreaterThan(GraphPresentationMetrics.NormalNodeSize.y));
            Assert.That(GraphPresentationMetrics.NormalNodeSize.y, Is.EqualTo(GraphPresentationMetrics.ServiceNodeSize.y));
            Assert.That(GraphPresentationMetrics.LevelGap,
                Is.LessThan(GraphPresentationMetrics.NormalNodeSize.y));
        }

        [Test]
        public void Presentation_DecisionWidthExpandsWithOrderSlots()
        {
            Decision empty = Node<Decision>("Empty");
            Decision one = Node<Decision>("One");
            Decision many = Node<Decision>("Many");
            TestNode child = Node<TestNode>("Child");
            one.events = new[] { child.ToReference() };
            many.events = Enumerable.Range(0, 4).Select(_ => child.ToReference()).ToArray();
            BehaviourTreeData tree = Tree(empty, one, many, child);
            GraphTopology topology = GraphTopologyBuilder.Build(tree);

            Assert.That(GraphLayoutResolver.GetNodeSize(topology.FindNode(empty.uuid)),
                Is.EqualTo(new Vector2(176f, 76f)));
            Assert.That(GraphLayoutResolver.GetNodeSize(topology.FindNode(one.uuid)),
                Is.EqualTo(new Vector2(176f, 76f)));
            Assert.That(GraphLayoutResolver.GetNodeSize(topology.FindNode(many.uuid)),
                Is.EqualTo(new Vector2(456f, 76f)));
        }

        [Test]
        public void Presentation_NodeSizesIgnoreNamesAndReferenceCardinality()
        {
            TestNode shortNormal = Node<TestNode>("A");
            TestNode longNormal = Node<TestNode>(new string('N', 160));
            TestNode firstChild = Node<TestNode>("First Child");
            TestNode secondChild = Node<TestNode>("Second Child");
            longNormal.child = firstChild.ToReference();
            longNormal.raw = secondChild.ToRawReference();

            Sequence emptyFlow = Node<Sequence>("Empty Flow");
            Sequence populatedFlow = Node<Sequence>("Populated Flow");
            populatedFlow.events = new[] { firstChild.ToReference(), secondChild.ToReference() };

            BehaviourTreeData tree = Tree(
                shortNormal,
                longNormal,
                emptyFlow,
                populatedFlow,
                firstChild,
                secondChild);
            GraphTopology topology = GraphTopologyBuilder.Build(tree, includeRawReferences: true);

            Assert.That(
                GraphLayoutResolver.GetNodeSize(topology.FindNode(shortNormal.uuid)),
                Is.EqualTo(GraphLayoutResolver.GetNodeSize(topology.FindNode(longNormal.uuid))));
            Assert.That(
                GraphLayoutResolver.GetNodeSize(topology.FindNode(emptyFlow.uuid)),
                Is.EqualTo(GraphLayoutResolver.GetNodeSize(topology.FindNode(populatedFlow.uuid))));
        }

        [Test]
        public void Presentation_UsesCycleProxyAndKeepsRawReferenceExternal()
        {
            Sequence sequence = Node<Sequence>("Sequence");
            sequence.events = new[] { sequence.ToReference() };
            BehaviourTreeData cycleTree = Tree(sequence);
            GraphPresentation cyclePresentation = GraphPresentationBuilder.Build(GraphTopologyBuilder.Build(cycleTree));

            Assert.That(cyclePresentation.Roots.Count(item => item.TargetUUID == sequence.uuid), Is.EqualTo(1));
            GraphPresentationRelation cycleRelation = cyclePresentation.Relations.Single(relation =>
                relation.Kind == GraphPresentationRelationKind.SequenceStart);
            Assert.That(cycleRelation.TargetUUID, Is.EqualTo(sequence.uuid));

            TestHost head = Node<TestHost>("Host");
            TestNode child = Node<TestNode>("Child");
            head.children = new[] { child.ToReference() };
            head.raw = new RawNodeReference { UUID = child.uuid };
            BehaviourTreeData rawTree = Tree(head, child);
            GraphPresentation rawPresentation = GraphPresentationBuilder.Build(GraphTopologyBuilder.Build(rawTree, includeRawReferences: true));

            Assert.That(rawPresentation.Relations.Any(edge => edge.Kind == GraphPresentationRelationKind.Raw), Is.True);
        }

        [Test]
        public void Presentation_LayoutDoesNotRewriteNodeCoordinates()
        {
            Sequence sequence = Node<Sequence>("Sequence");
            TestNode child = Node<TestNode>("Child");
            sequence.events = new[] { child.ToReference() };
            BehaviourTreeData tree = Tree(sequence, child);
            GraphTopology topology = GraphTopologyBuilder.Build(tree);
            GraphLayoutResolver.Resolve(tree, topology);
            Vector2 original = topology.FindNode(child.uuid).Position;

            GraphPresentation presentation = GraphPresentationBuilder.Build(topology);
            GraphPresentationLayout.Layout(presentation);

            Assert.That(topology.FindNode(child.uuid).Position, Is.EqualTo(original));
            Assert.That(presentation.Find(sequence.uuid).Size, Is.EqualTo(GraphLayoutResolver.GetNodeSize(topology.FindNode(sequence.uuid))));
        }

        [Test]
        public void Presentation_DecoratorKeepsAuthoredChildEdgeAndUsesFinalCompletion()
        {
            Sequence sequence = Node<Sequence>("Sequence");
            Inverter outer = Node<Inverter>("Outer");
            Always inner = Node<Always>("Inner");
            TestNode child = Node<TestNode>("Child");
            TestNode next = Node<TestNode>("Next");
            sequence.events = new[] { outer.ToReference(), next.ToReference() };
            outer.node = inner.ToReference();
            inner.node = child.ToReference();

            GraphPresentation presentation = GraphPresentationBuilder.Build(
                GraphTopologyBuilder.Build(Tree(sequence, outer, inner, child, next)));
            GraphPresentationLayout.Layout(presentation);

            GraphPresentationItem outerItem = presentation.Find(outer.uuid);
            GraphPresentationItem innerItem = presentation.Find(inner.uuid);
            GraphPresentationItem childItem = presentation.Find(child.uuid);
            GraphPresentationRelation authored = presentation.Relations.Single(relation =>
                relation.Origin?.FieldName == nameof(Decorator.node) && relation.Source.Item == outerItem);
            Assert.That(authored.Role, Is.EqualTo(GraphPresentationRelationRole.AuthoredReference));
            Assert.That(authored.Source, Is.EqualTo(outerItem.Output));
            Assert.That(authored.Target, Is.EqualTo(innerItem.Entry));

            GraphPresentationRelation continuation = presentation.Relations.Single(relation =>
                relation.Kind == GraphPresentationRelationKind.SequenceNext);
            Assert.That(presentation.ResolveContinuationSource(continuation), Is.EqualTo(childItem.Completion));
            Assert.That(presentation.Find(next.uuid).Position.y, Is.GreaterThan(outerItem.Position.y));
        }

        [Test]
        public void Presentation_CompletionOwnerIsOutputForOrdinaryAndPlaceholder()
        {
            Inverter decorator = Node<Inverter>("Decorator");
            TestNode child = Node<TestNode>("Child");
            decorator.node = child.ToReference();
            GraphPresentation attached = GraphPresentationBuilder.Build(
                GraphTopologyBuilder.Build(Tree(decorator, child)));
            GraphPresentationLayout.Layout(attached);
            Assert.That(attached.Find(child.uuid).Completion.Anchor, Is.EqualTo(GraphPresentationAnchorKind.Output));

            Inverter empty = Node<Inverter>("Empty");
            empty.node = NodeReference.Empty;
            GraphPresentation placeholder = GraphPresentationBuilder.Build(
                GraphTopologyBuilder.Build(Tree(empty)));
            GraphPresentationLayout.Layout(placeholder);
            GraphPresentationItem anchor = placeholder.FindDecoratorStack(empty.uuid).Anchor;
            Assert.That(anchor.DecoratorPlaceholder, Is.Not.Null);
            Assert.That(anchor.Completion.Anchor, Is.EqualTo(GraphPresentationAnchorKind.Output));
        }

        [Test]
        public void Presentation_FlowChildCompletionIsFlowCompleteAndLayoutIsIdempotent()
        {
            Inverter decorator = Node<Inverter>("Decorator");
            Sequence flow = Node<Sequence>("Flow");
            TestNode child = Node<TestNode>("Child");
            flow.events = new[] { child.ToReference() };
            decorator.node = flow.ToReference();
            GraphPresentation presentation = GraphPresentationBuilder.Build(
                GraphTopologyBuilder.Build(Tree(decorator, flow, child)));

            GraphPresentationLayout.Layout(presentation);
            GraphDecoratorStack stack = presentation.FindDecoratorStack(decorator.uuid);
            GraphPresentationItem flowItem = presentation.Find(flow.uuid);
            Assert.That(flowItem.Completion.Anchor, Is.EqualTo(GraphPresentationAnchorKind.FlowComplete));
            Assert.That(presentation.Find(decorator.uuid).Completion, Is.EqualTo(flowItem.Completion));
            Rect firstBounds = stack.VisualBounds;
            Vector2 firstCompletion = flowItem.FlowScope.CompletionPosition;
            GraphPresentationLayout.Layout(presentation);
            Assert.That(stack.VisualBounds, Is.EqualTo(firstBounds));
            Assert.That(flowItem.FlowScope.CompletionPosition, Is.EqualTo(firstCompletion));
        }

    }
}
