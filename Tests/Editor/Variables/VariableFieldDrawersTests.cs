using Aethiumian.AI.Nodes;
using Aethiumian.AI.References;
using Aethiumian.AI.Tests.Support;
using Aethiumian.AI.Variables;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Aethiumian.AI.Editor.Tests.Variables
{
    /// <summary>Validates variable-row layout, compatibility menus, and the production mutation commit boundary.</summary>
    public sealed class VariableFieldDrawersTests
    {
        private static readonly MethodInfo ApplyMutation = typeof(VariableFieldDrawers).GetMethod(
            "ApplyMutation", BindingFlags.NonPublic | BindingFlags.Static);
        private static readonly MethodInfo CreateVariable = typeof(VariableFieldDrawers).GetMethod(
            "CreateVariable", BindingFlags.NonPublic | BindingFlags.Static,
            null, new[] { typeof(BehaviourTreeData), typeof(VariableFieldBase), typeof(VariableType), typeof(string) }, null);
        private const string ScriptVariableAssetGuid = "ddccad13be1840d4688d847231100ba3";

        [Test]
        public void CalculateRowLayout_WithoutActionDoesNotReserveOverflow()
        {
            VariableFieldDrawers.VariableRowLayout layout = VariableFieldDrawers.CalculateRowLayout(new Rect(0f, 0f, 220f, 17f), false);

            Assert.That(layout.HasOverflow, Is.False);
            Assert.That(layout.OverflowRect, Is.EqualTo(Rect.zero));
            Assert.That(layout.LabelRect.width + layout.ValueRect.width + layout.TypeRect.width, Is.GreaterThan(0f));
        }

        [TestCase(0f)]
        [TestCase(8f)]
        [TestCase(220f)]
        [TestCase(300f)]
        [TestCase(620f)]
        public void CalculateRowLayout_KeepsLabelValueTypeAndOverflowOrderedInsideTheRow(float width)
        {
            Rect input = new(10f, 20f, width, 17f);

            foreach (bool hasAction in new[] { false, true })
            {
                VariableFieldDrawers.VariableRowLayout layout = VariableFieldDrawers.CalculateRowLayout(input, hasAction);

                Assert.That(layout.LabelRect.width, Is.GreaterThanOrEqualTo(0f));
                Assert.That(layout.ValueRect.width, Is.GreaterThanOrEqualTo(0f));
                Assert.That(layout.TypeRect.width, Is.GreaterThanOrEqualTo(0f));
                Assert.That(layout.OverflowRect.width, Is.GreaterThanOrEqualTo(0f));
                Assert.That(layout.LabelRect.xMax, Is.LessThanOrEqualTo(layout.ValueRect.x));
                Assert.That(layout.ValueRect.xMax, Is.LessThanOrEqualTo(layout.TypeRect.x));
                Assert.That(layout.TypeRect.xMax, Is.LessThanOrEqualTo(input.xMax));
                Assert.That(layout.OverflowRect.xMax, Is.LessThanOrEqualTo(input.xMax));
                Assert.That(layout.LabelRect.Overlaps(layout.ValueRect), Is.False);
                Assert.That(layout.ValueRect.Overlaps(layout.TypeRect), Is.False);
                Assert.That(layout.TypeRect.Overlaps(layout.OverflowRect), Is.False);
                if (hasAction == false)
                {
                    Assert.That(layout.OverflowRect, Is.EqualTo(Rect.zero));
                }
            }
        }

        [TestCase(220f, 90f)]
        [TestCase(300f, 120f)]
        [TestCase(620f, 350f)]
        public void CalculateRowLayout_KeepsTheTypeColumnCompactAndGivesTheRestToTheValue(float width, float minimumValueWidth)
        {
            VariableFieldDrawers.VariableRowLayout layout = VariableFieldDrawers.CalculateRowLayout(new Rect(0f, 0f, width, 17f), true);

            Assert.That(layout.TypeRect.width, Is.EqualTo(64f), "The type column is a compact fixed category column, so the value keeps the row's remaining space.");
            Assert.That(layout.ValueRect.width, Is.GreaterThanOrEqualTo(minimumValueWidth), "The value column keeps the space the variable name needs.");
            Assert.That(layout.LabelRect.width, Is.LessThanOrEqualTo(width * 0.25f), "The label never takes more than a quarter of the row.");
            Assert.That(layout.LabelRect.width, Is.LessThanOrEqualTo(90f), "The label is capped at 90px.");
            Assert.That(layout.OverflowRect.width, Is.EqualTo(GraphInspectorLayout.OverflowWidth));
        }

        [Test]
        public void CalculateRowLayout_WithoutActionDoesNotReserveOverflowAndWidensTheValue()
        {
            VariableFieldDrawers.VariableRowLayout withAction = VariableFieldDrawers.CalculateRowLayout(new Rect(0f, 0f, 220f, 17f), true);
            VariableFieldDrawers.VariableRowLayout withoutAction = VariableFieldDrawers.CalculateRowLayout(new Rect(0f, 0f, 220f, 17f), false);

            Assert.That(withoutAction.HasOverflow, Is.False);
            Assert.That(withoutAction.OverflowRect, Is.EqualTo(Rect.zero));
            Assert.That(withoutAction.ValueRect.width, Is.GreaterThan(withAction.ValueRect.width));
        }

        [Test]
        public void BuildTypeMenuOptions_HidesNodeAndInvalidAndMarksVisibleIncompatibleTypes()
        {
            VariableField field = new(VariableType.Int);
            VariableFieldDrawers.CurrentVariableType current = VariableFieldDrawers.ResolveCurrentVariableType(null, field);
            IReadOnlyList<VariableFieldDrawers.VariableTypeMenuOption> options = VariableFieldDrawers.BuildTypeMenuOptions(
                field,
                new[] { VariableType.Node, VariableType.Invalid, VariableType.Int, VariableType.Float, VariableType.String },
                typeof(int),
                VariableAccessFlag.Read,
                current);

            Assert.That(options.Select(option => option.Type), Is.EqualTo(new[] {
                VariableType.Int, VariableType.Float, VariableType.String }));
            Assert.That(options[0].IsCurrent, Is.True);
            Assert.That(options[0].IsCompatible, Is.True);
            Assert.That(options[1].IsCompatible, Is.True, "Int and Float have shared directional conversions.");
            Assert.That(options[2].IsCompatible, Is.False, "String is visible but cannot be read as Int.");
        }

        [Test]
        public void BuildTypeMenuOptions_DoesNotReAddTheHiddenCurrentType()
        {
            VariableField field = new(VariableType.String);
            VariableFieldDrawers.CurrentVariableType current = VariableFieldDrawers.ResolveCurrentVariableType(null, field);
            IReadOnlyList<VariableFieldDrawers.VariableTypeMenuOption> options = VariableFieldDrawers.BuildTypeMenuOptions(
                field,
                new[] { VariableType.Int },
                typeof(string),
                VariableAccessFlag.Read,
                current);

            Assert.That(options.Select(option => option.Type), Is.EqualTo(new[] { VariableType.Int }));
            Assert.That(options[0].IsCurrent, Is.False);
        }

        [Test]
        public void CanSwitchConstantType_OnlyAllowsUnboundNonGenericValueFields()
        {
            VariableData reference = new("Existing", VariableType.Int);
            VariableField dynamicField = new(VariableType.Int);
            VariableField boundField = new(VariableType.Int);
            boundField.SetReference(reference);

            Assert.That(VariableFieldDrawers.CanSwitchConstantType(dynamicField), Is.True);
            Assert.That(VariableFieldDrawers.CanSwitchConstantType(boundField), Is.False);
            Assert.That(VariableFieldDrawers.CanSwitchConstantType(new VariableField<int>()), Is.False);
            Assert.That(VariableFieldDrawers.CanSwitchConstantType(new VariableReference<int>()), Is.False);
            Assert.That(VariableFieldDrawers.CanSwitchConstantType(new Parameter(VariableType.Int)), Is.False);
        }

        [Test]
        public void GetCompatibleVariables_UsesRealClrConversionsInsteadOfCategoryEquality()
        {
            VariableData integer = new("Integer", VariableType.Int);
            VariableData real = new("Real", VariableType.Float);
            VariableData text = new("Text", VariableType.String);
            List<VariableData> all = new() { integer, real, text };

            List<VariableData> candidates = VariableFieldDrawers.GetCompatibleVariables(
                null,
                VariableTypeCatalog.GetAllVariableTypes(),
                typeof(int),
                VariableAccessFlag.Read,
                all);

            Assert.That(candidates, Is.EqualTo(new[] { integer, real }));
        }

        [Test]
        public void GetCompatibleVariables_KeepsAnIncompatibleBoundVariableOutOfCandidates()
        {
            VariableReference<int> field = new();
            VariableData compatible = new("Integer", VariableType.Int);
            VariableData incompatible = new("Text", VariableType.String);
            field.SetReference(incompatible);

            List<VariableData> candidates = VariableFieldDrawers.GetCompatibleVariables(
                null,
                VariableTypeCatalog.GetAllVariableTypes(),
                typeof(int),
                VariableAccessFlag.Read,
                new List<VariableData> { compatible, incompatible });

            Assert.That(candidates, Is.EqualTo(new[] { compatible }));
            Assert.That(field.UUID, Is.EqualTo(incompatible.UUID), "An incompatible authored binding must remain authored until the user changes it.");
        }

        [Test]
        public void GetCompatibleVariables_RespectsVisibilityBeforeCompatibility()
        {
            VariableData integer = new("Integer", VariableType.Int);
            VariableData real = new("Real", VariableType.Float);

            List<VariableData> candidates = VariableFieldDrawers.GetCompatibleVariables(
                null,
                new[] { VariableType.Int },
                typeof(int),
                VariableAccessFlag.Read,
                new List<VariableData> { integer, real });

            Assert.That(candidates, Is.EqualTo(new[] { integer }));
        }

        [Test]
        public void ResolveCurrentVariableType_UsesTheBoundVariableClrInsteadOfTheConsumerType()
        {
            BehaviourTreeData tree = ScriptableObject.CreateInstance<BehaviourTreeData>();
            try
            {
                VariableData integer = new("Integer", VariableType.Int);
                tree.variables.Add(integer);
                VariableReference<float> field = new();
                field.SetReference(integer);

                VariableFieldDrawers.CurrentVariableType current = VariableFieldDrawers.ResolveCurrentVariableType(tree, field);

                Assert.That(field.Type, Is.EqualTo(VariableType.Float), "The field still consumes Float.");
                Assert.That(current.ActualType, Is.EqualTo(typeof(int)));
                Assert.That(current.Category, Is.EqualTo(VariableType.Int));

                IReadOnlyList<VariableFieldDrawers.VariableTypeMenuOption> options = VariableFieldDrawers.BuildTypeMenuOptions(
                    field,
                    new[] { VariableType.Int, VariableType.Float },
                    typeof(float),
                    VariableAccessFlag.Read,
                    current);
                Assert.That(options.Single(option => option.Type == VariableType.Int).IsCurrent, Is.True);
                Assert.That(options.Single(option => option.Type == VariableType.Float).IsCurrent, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tree);
            }
        }

        [Test]
        public void ResolveCurrentVariableType_MissingBindingDoesNotPretendTheConsumerTypeIsCurrent()
        {
            BehaviourTreeData tree = ScriptableObject.CreateInstance<BehaviourTreeData>();
            try
            {
                VariableReference<int> field = new();
                field.SetReference(new VariableData("Missing", VariableType.Int));

                VariableFieldDrawers.CurrentVariableType current = VariableFieldDrawers.ResolveCurrentVariableType(tree, field);
                IReadOnlyList<VariableFieldDrawers.VariableTypeMenuOption> options = VariableFieldDrawers.BuildTypeMenuOptions(
                    field,
                    new[] { VariableType.Int },
                    typeof(int),
                    VariableAccessFlag.All,
                    current);

                Assert.That(current.ActualType, Is.Null);
                Assert.That(options, Is.Not.Empty);
                Assert.That(options.All(option => option.IsCurrent), Is.False);
                Assert.That(field.UUID, Is.Not.EqualTo(UUID.Empty), "The missing binding UUID must be preserved.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tree);
            }
        }

        [Test]
        public void ScriptSetterBinding_DisplaysItsRealIntTypeAndFiltersByRealAccess()
        {
            BehaviourTreeData tree = ScriptableObject.CreateInstance<BehaviourTreeData>();
            GameObject scriptHost = null;
            try
            {
                scriptHost = ConfigureScriptTarget(tree);
                VariableData setter = CreateScriptVariable("Setter", nameof(ScriptVariable.value5), VariableType.Generic);
                tree.variables.Add(setter);
                VariableReference<int> readField = new();
                readField.SetReference(setter);

                VariableFieldDrawers.CurrentVariableType current = VariableFieldDrawers.ResolveCurrentVariableType(tree, readField);
                Assert.That(current.ActualType, Is.EqualTo(typeof(int)), "A write-only member still has a real Int type.");
                Assert.That(current.Category, Is.EqualTo(VariableType.Int));

                List<VariableData> all = new() { setter };
                Assert.That(
                    VariableFieldDrawers.GetCompatibleVariables(tree, new[] { VariableType.Int }, typeof(int), VariableAccessFlag.Write, all),
                    Is.EqualTo(new[] { setter }));
                Assert.That(
                    VariableFieldDrawers.GetCompatibleVariables(tree, new[] { VariableType.Int }, typeof(int), VariableAccessFlag.Read, all),
                    Is.Empty);
            }
            finally
            {
                if (scriptHost != null)
                {
                    UnityEngine.Object.DestroyImmediate(scriptHost);
                }
                UnityEngine.Object.DestroyImmediate(tree);
            }
        }

        [Test]
        public void StandardTargetScript_UsesTheConfiguredRealClassOrStaysUnresolved()
        {
            BehaviourTreeData tree = ScriptableObject.CreateInstance<BehaviourTreeData>();
            GameObject scriptHost = null;
            try
            {
                scriptHost = ConfigureScriptTarget(tree);
                VariableReference<UnityEngine.Object> field = new();
                field.SetReference(VariableData.TargetScriptVariable);
                List<VariableData> all = new() { VariableData.TargetScriptVariable };

                VariableFieldDrawers.CurrentVariableType current = VariableFieldDrawers.ResolveCurrentVariableType(tree, field);
                Assert.That(current.ActualType, Is.EqualTo(typeof(ScriptVariable)));
                Assert.That(current.Category, Is.EqualTo(VariableType.UnityObject));
                Assert.That(
                    VariableFieldDrawers.GetCompatibleVariables(tree, new[] { VariableType.UnityObject }, typeof(ScriptVariable), VariableAccessFlag.Read, all),
                    Is.EqualTo(new[] { VariableData.TargetScriptVariable }));

                tree.targetScript = null;
                Assert.That(VariableFieldDrawers.ResolveCurrentVariableType(tree, field).ActualType, Is.Null);
                Assert.That(
                    VariableFieldDrawers.GetCompatibleVariables(tree, new[] { VariableType.UnityObject }, typeof(ScriptVariable), VariableAccessFlag.Read, all),
                    Is.Empty);
            }
            finally
            {
                if (scriptHost != null)
                {
                    UnityEngine.Object.DestroyImmediate(scriptHost);
                }
                UnityEngine.Object.DestroyImmediate(tree);
            }
        }

        [Test]
        public void BuildVariableSelectionOptions_KeepsAMissingBindingAndStillOffersCandidates()
        {
            VariableData candidate = new("Integer", VariableType.Int);
            UUID missingUuid = UUID.NewUUID();

            VariableFieldDrawers.VariableSelectionOptions selection = VariableFieldDrawers.BuildVariableSelectionOptions(
                null,
                missingUuid,
                null,
                new List<VariableData> { candidate },
                true,
                "Create New…");

            Assert.That(selection.CurrentIndex, Is.EqualTo(0));
            Assert.That(selection.Options[0].Kind, Is.EqualTo(VariableFieldDrawers.VariableSelectionKind.InvalidCurrent));
            Assert.That(selection.Options[0].Content.text, Is.EqualTo("Missing"), "The row stays short instead of spending its width on the UUID.");
            Assert.That(selection.Options[0].Content.tooltip, Does.Contain(missingUuid.Value), "The tooltip carries the full missing UUID.");
            Assert.That(selection.Options[0].Content.tooltip, Does.Contain("no longer exists"), "The tooltip carries the missing-binding reason.");
            Assert.That(selection.Options[0].Content.image, Is.Not.Null, "The preserved invalid binding keeps a lightweight warning icon.");
            Assert.That(selection.Options.Any(option => option.Kind == VariableFieldDrawers.VariableSelectionKind.None), Is.True);
            Assert.That(
                selection.Options.Any(option => option.Kind == VariableFieldDrawers.VariableSelectionKind.Candidate && option.Uuid == candidate.UUID),
                Is.True);
            Assert.That(selection.Options[selection.Options.Count - 1].Kind, Is.EqualTo(VariableFieldDrawers.VariableSelectionKind.Create));
        }

        [Test]
        public void BuildVariableSelectionOptions_KeepsAnExcludedBindingReadableAndOmitsCreateWithoutAType()
        {
            VariableData bound = new("Text", VariableType.String);
            VariableData candidate = new("Integer", VariableType.Int);

            VariableFieldDrawers.VariableSelectionOptions selection = VariableFieldDrawers.BuildVariableSelectionOptions(
                null,
                bound.UUID,
                bound,
                new List<VariableData> { candidate },
                false,
                "Create New…");

            Assert.That(selection.CurrentIndex, Is.EqualTo(0));
            Assert.That(selection.Options[0].Kind, Is.EqualTo(VariableFieldDrawers.VariableSelectionKind.InvalidCurrent));
            Assert.That(selection.Options[0].Content.text, Is.EqualTo(bound.name), "The name keeps its row space instead of gaining an (Incompatible) suffix.");
            Assert.That(selection.Options[0].Content.text, Does.Not.Contain("Incompatible"));
            Assert.That(selection.Options[0].Content.tooltip, Does.Contain(bound.name));
            Assert.That(selection.Options[0].Content.tooltip, Does.Contain(bound.UUID.Value));
            Assert.That(selection.Options[0].Content.tooltip, Does.Contain("hidden"), "The tooltip owns the incompatible/hidden reason.");
            Assert.That(selection.Options[0].Content.image, Is.Not.Null, "The preserved invalid binding keeps a lightweight warning icon.");
            Assert.That(
                selection.Options.Any(option => option.Kind == VariableFieldDrawers.VariableSelectionKind.Candidate && option.Uuid == candidate.UUID),
                Is.True);
            Assert.That(selection.Options.Any(option => option.Kind == VariableFieldDrawers.VariableSelectionKind.Create), Is.False);
        }

        [Test]
        public void ResolveCreationType_ReturnsNullWhenNoCategoryCanCreateACompatibleVariable()
        {
            VariableField<int> field = 7;

            Assert.That(
                VariableFieldDrawers.ResolveCreationType(field, new[] { VariableType.Int }, typeof(NoConversionValue), VariableAccessFlag.All),
                Is.Null);
            Assert.That(
                VariableFieldDrawers.ResolveCreationType(field, Array.Empty<VariableType>(), typeof(int), VariableAccessFlag.All),
                Is.Null);
        }

        [Test]
        public void ResolveCreationType_JudgesTheCreatedObjectTypeInsteadOfTheGenericFieldArgument()
        {
            VariableField<NoConversionValue> field = new();

            Assert.That(
                VariableFieldDrawers.ResolveCreationType(field, new[] { VariableType.Generic }, typeof(NoConversionValue), VariableAccessFlag.Write),
                Is.Null,
                "A Generic variable is created as object, so it cannot be written into a struct even when the field's T is that struct.");
        }

        [Test]
        public void ResolveCreationType_PrefersTheCurrentCompatibleCategoryWithoutFallingBackToFloat()
        {
            VariableField<int> integerConstant = 7;
            VariableField<float> floatConstant = 1f;
            VariableType[] visible = { VariableType.Float, VariableType.Int };

            Assert.That(
                VariableFieldDrawers.ResolveCreationType(integerConstant, visible, typeof(float), VariableAccessFlag.Read),
                Is.EqualTo(VariableType.Int));
            Assert.That(
                VariableFieldDrawers.ResolveCreationType(floatConstant, visible, typeof(float), VariableAccessFlag.Read),
                Is.EqualTo(VariableType.Float));
        }

        [Test]
        public void ResolveCreationType_CreatesTheCanonicalTypeItValidated()
        {
            VariableField<DrawerEnum> enumField = new();
            BehaviourTreeData enumTree = ScriptableObject.CreateInstance<BehaviourTreeData>();
            BehaviourTreeData objectTree = ScriptableObject.CreateInstance<BehaviourTreeData>();
            try
            {
                VariableType? enumCreation = VariableFieldDrawers.ResolveCreationType(
                    enumField, VariableTypeCatalog.GetAllVariableTypes(), typeof(DrawerEnum), VariableAccessFlag.Read);
                Assert.That(enumCreation, Is.EqualTo(VariableType.Int));
                InvokeCreateVariable(enumTree, enumField, enumCreation.Value);
                VariableData createdEnum = enumTree.variables.Single();
                Assert.That(createdEnum.ObjectType, Is.EqualTo(VariableUtility.GetType(enumCreation.Value)));
                Assert.That(VariableTypeCatalog.SupportsAccess(createdEnum.ObjectType, typeof(DrawerEnum), VariableAccessFlag.Read), Is.True);
                Assert.That(enumField.UUID, Is.EqualTo(createdEnum.UUID));

                VariableField<GameObject> objectField = new();
                VariableType? objectCreation = VariableFieldDrawers.ResolveCreationType(
                    objectField, VariableTypeCatalog.GetAllVariableTypes(), typeof(GameObject), VariableAccessFlag.All);
                Assert.That(objectCreation, Is.EqualTo(VariableType.UnityObject));
                InvokeCreateVariable(objectTree, objectField, objectCreation.Value);
                VariableData createdObject = objectTree.variables.Single();
                Assert.That(createdObject.ObjectType, Is.EqualTo(typeof(UnityEngine.Object)));
                Assert.That(VariableTypeCatalog.SupportsAccess(createdObject.ObjectType, typeof(GameObject), VariableAccessFlag.All), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(enumTree);
                UnityEngine.Object.DestroyImmediate(objectTree);
            }
        }

        [Test]
        public void BuildCurrentTypeTooltip_ExplainsHiddenAndUnsupportedReasons()
        {
            VariableReference<int> field = new();
            VariableFieldDrawers.CurrentVariableType current = VariableFieldDrawers.ResolveCurrentVariableType(null, field);

            string hiddenAndReadOnly = VariableFieldDrawers.BuildCurrentTypeTooltip(
                field, current, new[] { VariableType.Float }, typeof(int), VariableAccessFlag.All);
            Assert.That(hiddenAndReadOnly, Does.Contain("Hidden by constraints."));

            string unsupported = VariableFieldDrawers.BuildCurrentTypeTooltip(
                field, current, new[] { VariableType.Int }, typeof(NoConversionValue), VariableAccessFlag.All);
            Assert.That(unsupported, Does.Contain("Unsupported"));
            Assert.That(unsupported, Does.Not.Contain("not compatible"), "The failing direction is marked instead of adding a separate summary.");

            BehaviourTreeData tree = ScriptableObject.CreateInstance<BehaviourTreeData>();
            try
            {
                VariableReference<int> missing = new();
                missing.SetReference(new VariableData("Missing", VariableType.Int));
                string unresolved = VariableFieldDrawers.BuildCurrentTypeTooltip(
                    missing,
                    VariableFieldDrawers.ResolveCurrentVariableType(tree, missing),
                    new[] { VariableType.Int },
                    typeof(int),
                    VariableAccessFlag.All);
                Assert.That(unresolved, Does.Contain("Missing or unsupported"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tree);
            }
        }

        [Test]
        public void GetTypeMenuOptionState_SeparatesSwitchableViewOnlyAndDisabledEntries()
        {
            VariableField constant = new(VariableType.Int);
            VariableFieldDrawers.CurrentVariableType constantCurrent = VariableFieldDrawers.ResolveCurrentVariableType(null, constant);
            IReadOnlyList<VariableFieldDrawers.VariableTypeMenuOption> constantOptions = VariableFieldDrawers.BuildTypeMenuOptions(
                constant,
                new[] { VariableType.Int, VariableType.Float, VariableType.String },
                typeof(int),
                VariableAccessFlag.All,
                constantCurrent);

            Assert.That(
                VariableFieldDrawers.GetTypeMenuOptionState(constantOptions.Single(option => option.Type == VariableType.Int), true),
                Is.EqualTo(VariableFieldDrawers.TypeMenuOptionState.Current));
            Assert.That(
                VariableFieldDrawers.GetTypeMenuOptionState(constantOptions.Single(option => option.Type == VariableType.Float), true),
                Is.EqualTo(VariableFieldDrawers.TypeMenuOptionState.Switchable));
            Assert.That(
                VariableFieldDrawers.GetTypeMenuOptionState(constantOptions.Single(option => option.Type == VariableType.String), true),
                Is.EqualTo(VariableFieldDrawers.TypeMenuOptionState.Disabled));

            VariableReference<int> reference = new();
            IReadOnlyList<VariableFieldDrawers.VariableTypeMenuOption> referenceOptions = VariableFieldDrawers.BuildTypeMenuOptions(
                reference,
                new[] { VariableType.Int, VariableType.Float, VariableType.String },
                typeof(int),
                VariableAccessFlag.All,
                VariableFieldDrawers.ResolveCurrentVariableType(null, reference));

            Assert.That(VariableFieldDrawers.CanSwitchConstantType(reference), Is.False);
            Assert.That(
                VariableFieldDrawers.GetTypeMenuOptionState(referenceOptions.Single(option => option.Type == VariableType.Int), false),
                Is.EqualTo(VariableFieldDrawers.TypeMenuOptionState.Current));
            Assert.That(
                VariableFieldDrawers.GetTypeMenuOptionState(referenceOptions.Single(option => option.Type == VariableType.Float), false),
                Is.EqualTo(VariableFieldDrawers.TypeMenuOptionState.ViewOnly));
            Assert.That(
                VariableFieldDrawers.GetTypeMenuOptionState(referenceOptions.Single(option => option.Type == VariableType.String), false),
                Is.EqualTo(VariableFieldDrawers.TypeMenuOptionState.Disabled));
        }

        [TestCase(VariableAccessFlag.Read, true, false)]
        [TestCase(VariableAccessFlag.Write, false, true)]
        [TestCase(VariableAccessFlag.All, true, true)]
        [TestCase(VariableAccessFlag.None, true, true)]
        public void BuildCurrentTypeTooltip_ShowsOnlyTheAccessDirectionsTheFieldNeeds(
            VariableAccessFlag access,
            bool expectsRead,
            bool expectsWrite)
        {
            VariableReference<float> field = new();
            VariableFieldDrawers.CurrentVariableType current = VariableFieldDrawers.ResolveCurrentVariableType(null, field);

            string tooltip = VariableFieldDrawers.BuildCurrentTypeTooltip(
                field, current, VariableTypeCatalog.GetAllVariableTypes(), typeof(int), access);

            Assert.That(tooltip.Split('\n')[0], Is.EqualTo("System.Single"), "The first line is the true CLR actual type.");
            Assert.That(tooltip.Contains("Read: Float → Int"), Is.EqualTo(expectsRead));
            Assert.That(tooltip.Contains("Write: Int → Float"), Is.EqualTo(expectsWrite));
        }

        [Test]
        public void BuildCurrentTypeTooltip_CondensesIdentityAndExplainsUnsupportedDirections()
        {
            VariableReference<int> field = new();
            VariableFieldDrawers.CurrentVariableType current = VariableFieldDrawers.ResolveCurrentVariableType(null, field);

            string identity = VariableFieldDrawers.BuildCurrentTypeTooltip(
                field, current, new[] { VariableType.Int }, typeof(int), VariableAccessFlag.All);
            Assert.That(identity, Is.EqualTo("System.Int32"));

            string unsupported = VariableFieldDrawers.BuildCurrentTypeTooltip(
                field, current, new[] { VariableType.Int }, typeof(NoConversionValue), VariableAccessFlag.All);
            Assert.That(unsupported, Does.Contain("Read: Int → NoConversionValue"));
            Assert.That(unsupported, Does.Contain("Write: NoConversionValue → Int"));
            Assert.That(unsupported.Split('\n').Count(line => line.Contains("Unsupported")), Is.EqualTo(2),
                "Each unavailable direction is marked without a separate summary paragraph.");
        }

        [Test]
        public void BuildCurrentTypeTooltip_DoesNotFabricateAConversionWithoutAConsumer()
        {
            VariableReference<object> field = new();
            VariableFieldDrawers.CurrentVariableType current = VariableFieldDrawers.ResolveCurrentVariableType(null, field);

            string tooltip = VariableFieldDrawers.BuildCurrentTypeTooltip(
                field, current, VariableTypeCatalog.GetAllVariableTypes(), null, VariableAccessFlag.All);

            Assert.That(tooltip.Split('\n')[0], Is.EqualTo("System.Object"));
            Assert.That(tooltip, Does.Not.Contain("→"), "No explicit consumer means no direction line, including no fabricated Object conversion.");
            Assert.That(tooltip, Does.Not.Contain("Runtime type"));
            Assert.That(tooltip, Does.Not.Contain("Unsupported"));
        }

        [Test]
        public void BuildCurrentTypeTooltip_UsesTheActualScriptAndUnityTypesForTheirConversionConditions()
        {
            BehaviourTreeData tree = ScriptableObject.CreateInstance<BehaviourTreeData>();
            GameObject scriptHost = null;
            try
            {
                scriptHost = ConfigureScriptTarget(tree);

                VariableData vector = CreateScriptVariable("Vector", nameof(ScriptVariable.value1), VariableType.Vector3);
                tree.variables.Add(vector);
                VariableReference<Vector2> vectorField = new();
                vectorField.SetReference(vector);

                string vectorTooltip = VariableFieldDrawers.BuildCurrentTypeTooltip(
                    vectorField,
                    VariableFieldDrawers.ResolveCurrentVariableType(tree, vectorField),
                    VariableTypeCatalog.GetAllVariableTypes(),
                    typeof(Vector2),
                    VariableAccessFlag.Read);
                Assert.That(vectorTooltip.Split('\n')[0], Is.EqualTo(typeof(Vector3).FullName),
                    "The script member's real type is explained instead of the field's consumer type.");
                Assert.That(vectorTooltip, Does.Contain("Read: Vector3 → Vector2"));
                Assert.That(vectorTooltip, Does.Contain("Z discarded"));

                VariableReference<Transform> componentField = new();
                componentField.SetReference(VariableData.GameObjectVariable);

                string componentTooltip = VariableFieldDrawers.BuildCurrentTypeTooltip(
                    componentField,
                    VariableFieldDrawers.ResolveCurrentVariableType(tree, componentField),
                    VariableTypeCatalog.GetAllVariableTypes(),
                    typeof(Transform),
                    VariableAccessFlag.Read);
                Assert.That(componentTooltip.Split('\n')[0], Is.EqualTo(typeof(GameObject).FullName));
                Assert.That(componentTooltip, Does.Contain("Read: GameObject → Transform"),
                    "The direction keeps the real component type names instead of collapsing both to Object.");
                Assert.That(componentTooltip, Does.Contain("missing component"));
            }
            finally
            {
                if (scriptHost != null)
                {
                    UnityEngine.Object.DestroyImmediate(scriptHost);
                }
                UnityEngine.Object.DestroyImmediate(tree);
            }
        }

        [Test]
        public void DynamicConstantTypeMenu_HidesGenericButReferenceInfoStillShowsIt()
        {
            VariableField constant = new(VariableType.Int);
            IReadOnlyList<VariableFieldDrawers.VariableTypeMenuOption> constantOptions = VariableFieldDrawers.BuildTypeMenuOptions(
                constant,
                VariableTypeCatalog.GetAllVariableTypes(),
                null,
                VariableAccessFlag.All,
                VariableFieldDrawers.ResolveCurrentVariableType(null, constant));
            Assert.That(constantOptions.Any(option => option.Type == VariableType.Generic), Is.False);

            VariableReference reference = new() { type = VariableType.Generic };
            IReadOnlyList<VariableFieldDrawers.VariableTypeMenuOption> referenceOptions = VariableFieldDrawers.BuildTypeMenuOptions(
                reference,
                VariableTypeCatalog.GetAllVariableTypes(),
                null,
                VariableAccessFlag.All,
                VariableFieldDrawers.ResolveCurrentVariableType(null, reference));
            Assert.That(referenceOptions.Any(option => option.Type == VariableType.Generic), Is.True);
        }

        [Test]
        public void TimerAndCountdownNodeFields_RestrictTheTypeMenuToFloat()
        {
            AssertFloatOnlyTypeMenu(typeof(Cooldown), nameof(Cooldown.timer));
            AssertFloatOnlyTypeMenu(typeof(Throttle), nameof(Throttle.timer));
            AssertFloatOnlyTypeMenu(typeof(Countdown), nameof(Countdown.updatingVariable));

            FieldInfo ordinaryFloat = typeof(Cooldown).GetField(nameof(Cooldown.duration));
            Assert.That(
                VariableFieldEditorMetadata.GetAllowedTypes(ordinaryFloat),
                Is.EqualTo(VariableTypeCatalog.GetAllVariableTypes()),
                "An ordinary Float field keeps every category visible.");
        }

        [Test]
        public void DrawerInspectionHelpers_DoNotMutateAuthoredData()
        {
            BehaviourTreeData tree = ScriptableObject.CreateInstance<BehaviourTreeData>();
            try
            {
                VariableData data = new("Integer", VariableType.Int);
                tree.variables.Add(data);
                VariableReference<int> field = new();
                field.SetReference(data);
                VariableField dynamicConstant = new(VariableType.Int);
                UUID fieldUuid = field.UUID;
                VariableType dataType = data.Type;
                Type dataObjectType = data.ObjectType;
                int variableCount = tree.variables.Count;
                EditorUtility.ClearDirty(tree);

                VariableFieldDrawers.CurrentVariableType current = VariableFieldDrawers.ResolveCurrentVariableType(tree, field);
                IReadOnlyList<VariableFieldDrawers.VariableTypeMenuOption> options = VariableFieldDrawers.BuildTypeMenuOptions(
                    field, VariableTypeCatalog.GetAllVariableTypes(), typeof(int), VariableAccessFlag.All, current);
                IReadOnlyList<VariableFieldDrawers.VariableTypeMenuOption> constantOptions = VariableFieldDrawers.BuildTypeMenuOptions(
                    dynamicConstant,
                    VariableTypeCatalog.GetAllVariableTypes(),
                    null,
                    VariableAccessFlag.All,
                    VariableFieldDrawers.ResolveCurrentVariableType(tree, dynamicConstant));
                List<VariableData> candidates = VariableFieldDrawers.GetCompatibleVariables(
                    tree, VariableTypeCatalog.GetAllVariableTypes(), typeof(int), VariableAccessFlag.All, tree.variables.ToList());
                _ = VariableFieldDrawers.BuildVariableSelectionOptions(tree, field.UUID, data, candidates, true, "Create New…");
                _ = VariableFieldDrawers.BuildCurrentTypeTooltip(field, current, VariableTypeCatalog.GetAllVariableTypes(), typeof(int), VariableAccessFlag.All);
                _ = VariableFieldDrawers.GetTypeMenuOptionState(options.First(), false);
                _ = constantOptions;

                Assert.That(field.UUID, Is.EqualTo(fieldUuid));
                Assert.That(data.Type, Is.EqualTo(dataType));
                Assert.That(data.ObjectType, Is.EqualTo(dataObjectType));
                Assert.That(dynamicConstant.Type, Is.EqualTo(VariableType.Int));
                Assert.That(tree.variables.Count, Is.EqualTo(variableCount));
                Assert.That(EditorUtility.IsDirty(tree), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tree);
            }
        }

        [Test]
        public void VariableFieldDrawers_GetVariableHeight_RemainsSingleLineForPseudoProbabilityWeight()
        {
            VariableField<int> weight = new();

            Assert.That(VariableFieldDrawers.GetVariableHeight(weight, null),
                Is.EqualTo(EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing));
        }

        [Test]
        public void GetRuntimeSummary_ResolvedBindingDoesNotMutateRuntimeReference()
        {
            VariableData data = new("Runtime value", VariableType.Int);
            VariableField<int> field = new();
            TreeVariable runtimeVariable = new(data);
            runtimeVariable.SetValue(11);
            field.SetReference(data);
            field.SetRuntimeReference(runtimeVariable);

            string summary = VariableFieldDrawers.GetRuntimeSummary(field);

            Assert.That(summary, Does.Contain("Runtime value"));
            Assert.That(summary, Does.Contain(data.UUID.Value));
            Assert.That(summary, Does.Contain("11"));
            Assert.That(field.UUID, Is.EqualTo(data.UUID));
            Assert.That(field.RuntimeVariable, Is.SameAs(runtimeVariable));
            Assert.That(field.GetValue<int>(), Is.EqualTo(11));
        }

        [Test]
        public void GetRuntimeSummary_UnresolvedBindingDoesNotReadOrMutateField()
        {
            VariableData data = new("Missing value", VariableType.Int);
            VariableField<int> field = new();
            field.SetReference(data);

            string summary = null;
            Assert.DoesNotThrow(() => summary = VariableFieldDrawers.GetRuntimeSummary(field));

            Assert.That(summary, Does.Contain("Unresolved"));
            Assert.That(summary, Does.Contain(data.UUID.Value));
            Assert.That(field.UUID, Is.EqualTo(data.UUID));
            Assert.That(field.RuntimeVariable, Is.Null);
            Assert.That(() => field.GetValue<int>(), Throws.TypeOf<InvalidOperationException>());
        }

        [Test]
        public void GetRuntimeSummary_ConstantFieldShowsValueWithoutMutation()
        {
            VariableField<int> field = 7;

            string summary = VariableFieldDrawers.GetRuntimeSummary(field);

            Assert.That(summary, Does.Contain("Constant"));
            Assert.That(summary, Does.Contain("7"));
            Assert.That(field.HasEditorReference, Is.False);
            Assert.That(field.GetValue<int>(), Is.EqualTo(7));
        }

        [Test]
        public void ApplyMutationCommitPath_UseVariable_UndoRedoRestoresReferenceAndDirty()
        {
            VariableData existing = new("Existing", VariableType.Int);
            VariableMutationHost host = CreateHost(new VariableField(VariableType.Int));
            BehaviourTreeData tree = ScriptableObject.CreateInstance<BehaviourTreeData>();
            try
            {
                InvokeCommit(host, tree, value => value.SetReference(existing));
                AssertReference(host.value, existing);
                Assert.That(EditorUtility.IsDirty(host), Is.True);
                Undo.PerformUndo();
                Assert.That(host.value.HasEditorReference, Is.False);
                Undo.PerformRedo();
                AssertReference(host.value, existing);
            }
            finally { Destroy(host, tree); }
        }

        [Test]
        public void ApplyMutationCommitPath_CreateVariable_UndoRedoRestoresVariableAndReference()
        {
            VariableMutationHost host = CreateHost(new VariableField(VariableType.Int));
            BehaviourTreeData tree = ScriptableObject.CreateInstance<BehaviourTreeData>();
            try
            {
                int before = tree.EditorVariables.Count;
                InvokeCommit(host, tree, value => InvokeCreateVariable(tree, value));
                Assert.That(tree.EditorVariables.Count, Is.EqualTo(before + 1));
                Assert.That(host.value.HasEditorReference, Is.True);
                UUID created = host.value.UUID;
                Undo.PerformUndo();
                Assert.That(tree.EditorVariables.Count, Is.EqualTo(before));
                Assert.That(host.value.HasEditorReference, Is.False);
                Undo.PerformRedo();
                Assert.That(tree.EditorVariables.Count, Is.EqualTo(before + 1));
                Assert.That(host.value.UUID, Is.EqualTo(created));
            }
            finally { Destroy(host, tree); }
        }

        [Test]
        public void ApplyMutationCommitPath_RecreateInvalidReference_UndoRedoRestoresVariableAndReference()
        {
            VariableMutationHost host = CreateHost(new VariableField(VariableType.Int));
            host.value.SetReference(new VariableData("Missing", VariableType.Int));
            BehaviourTreeData tree = ScriptableObject.CreateInstance<BehaviourTreeData>();
            try
            {
                int before = tree.EditorVariables.Count;
                InvokeCommit(host, tree, value => InvokeCreateVariable(tree, value));
                Assert.That(tree.EditorVariables.Count, Is.EqualTo(before + 1));
                Assert.That(host.value.HasEditorReference, Is.True);
                Assert.That(host.value.UUID, Is.Not.EqualTo(UUID.Empty));
                Undo.PerformUndo();
                Assert.That(tree.EditorVariables.Count, Is.EqualTo(before));
                Assert.That(host.value.HasEditorReference, Is.True);
                Undo.PerformRedo();
                Assert.That(tree.EditorVariables.Count, Is.EqualTo(before + 1));
                Assert.That(host.value.HasEditorReference, Is.True);
            }
            finally { Destroy(host, tree); }
        }

        [Test]
        public void ApplyMutationCommitPath_SetConstant_UndoRedoRestoresReferenceState()
        {
            VariableData existing = new("Existing", VariableType.Int);
            VariableMutationHost host = CreateHost(new VariableField(VariableType.Int));
            host.value.SetReference(existing);
            BehaviourTreeData tree = ScriptableObject.CreateInstance<BehaviourTreeData>();
            try
            {
                InvokeCommit(host, tree, value => value.SetReference(null));
                Assert.That(host.value.HasEditorReference, Is.False);
                Undo.PerformUndo();
                AssertReference(host.value, existing);
                Undo.PerformRedo();
                Assert.That(host.value.HasEditorReference, Is.False);
            }
            finally { Destroy(host, tree); }
        }

        [Test]
        public void ApplyMutationCommitPath_Clear_UndoRedoRestoresReferenceState()
        {
            VariableData existing = new("Existing", VariableType.Int);
            VariableMutationHost host = CreateHost(new VariableField(VariableType.Int));
            host.value.SetReference(existing);
            BehaviourTreeData tree = ScriptableObject.CreateInstance<BehaviourTreeData>();
            try
            {
                InvokeCommit(host, tree, value => value.SetReference(null));
                Assert.That(host.value.HasEditorReference, Is.False);
                Undo.PerformUndo();
                AssertReference(host.value, existing);
                Undo.PerformRedo();
                Assert.That(host.value.HasEditorReference, Is.False);
            }
            finally { Destroy(host, tree); }
        }

        [Test]
        public void ApplyMutationCommitPath_ConstantType_UndoRedoRestoresType()
        {
            VariableField hostValue = new(VariableType.Float);
            VariableMutationHost host = CreateHost(hostValue);
            BehaviourTreeData tree = ScriptableObject.CreateInstance<BehaviourTreeData>();
            try
            {
                InvokeCommit(host, tree, value => ((VariableField)value).ForceSetConstantType(VariableType.Int));
                Assert.That(host.value.Type, Is.EqualTo(VariableType.Int));
                Undo.PerformUndo();
                Assert.That(host.value.Type, Is.EqualTo(VariableType.Float));
                Undo.PerformRedo();
                Assert.That(host.value.Type, Is.EqualTo(VariableType.Int));
            }
            finally { Destroy(host, tree); }
        }

        private static VariableMutationHost CreateHost(VariableField value)
        {
            VariableMutationHost host = ScriptableObject.CreateInstance<VariableMutationHost>();
            host.value = value;
            return host;
        }

        private static void InvokeCommit(VariableMutationHost host, BehaviourTreeData tree, Action<VariableFieldBase> mutation)
        {
            SerializedObject serializedObject = new(host);
            serializedObject.Update();
            serializedObject.FindProperty(nameof(VariableMutationHost.value)).boxedValue = host.value;
            serializedObject.ApplyModifiedProperties();
            ApplyMutation.Invoke(null, new object[] { tree, host, nameof(VariableMutationHost.value), host.value, mutation });
            serializedObject.Update();
            Assert.That(EditorUtility.IsDirty(tree), Is.True);
        }

        private static void InvokeCreateVariable(BehaviourTreeData tree, VariableFieldBase value)
        {
            CreateVariable.Invoke(null, new object[] { tree, value, value.Type, null });
        }

        private static void InvokeCreateVariable(BehaviourTreeData tree, VariableFieldBase value, VariableType type)
        {
            CreateVariable.Invoke(null, new object[] { tree, value, type, null });
        }

        private static GameObject ConfigureScriptTarget(BehaviourTreeData tree)
        {
            MonoScript scriptAsset = AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(ScriptVariableAssetGuid));
            Assert.That(scriptAsset, Is.Not.Null, "The target script fixture must be an imported MonoBehaviour MonoScript.");
            Assert.That(scriptAsset.GetClass(), Is.EqualTo(typeof(ScriptVariable)), "The target script fixture must resolve to its runtime CLR type.");

            GameObject host = new("VariableFieldDrawersScriptTarget");
            ScriptVariable script = host.AddComponent<ScriptVariable>();
            Assert.That(script, Is.Not.Null, "The target script fixture must attach to a GameObject at runtime.");
            tree.targetScript = scriptAsset;
            return host;
        }

        private static void AssertFloatOnlyTypeMenu(Type owner, string memberName)
        {
            FieldInfo member = owner.GetField(memberName);
            IReadOnlyList<VariableType> visibleTypes = VariableFieldEditorMetadata.GetAllowedTypes(member);
            VariableReference<float> field = new();
            IReadOnlyList<VariableFieldDrawers.VariableTypeMenuOption> options = VariableFieldDrawers.BuildTypeMenuOptions(
                field,
                visibleTypes,
                typeof(float),
                VariableAccessFlag.All,
                VariableFieldDrawers.ResolveCurrentVariableType(null, field));

            Assert.That(
                options.Select(option => option.Type),
                Is.EqualTo(new[] { VariableType.Float }),
                $"{owner.Name}.{memberName} must only offer Float.");
        }

        private static VariableData CreateScriptVariable(string name, string path, VariableType cachedType)
        {
            VariableData data = new(name, cachedType);
            data.SetScript(true);
            data.Path = path;
            return data;
        }

        private static void AssertReference(VariableFieldBase value, VariableData expected)
        {
            Assert.That(value.HasEditorReference, Is.True);
            Assert.That(value.UUID, Is.EqualTo(expected.UUID));
        }

        private static void Destroy(VariableMutationHost host, BehaviourTreeData tree)
        {
            UnityEngine.Object.DestroyImmediate(host);
            UnityEngine.Object.DestroyImmediate(tree);
            Undo.ClearAll();
        }

        private enum DrawerEnum
        {
            One,
            Two,
        }

        private readonly struct NoConversionValue
        {
        }

        /// <summary>Serialized host used only to exercise the production serialized commit boundary.</summary>
        private sealed class VariableMutationHost : ScriptableObject
        {
            public VariableField value;
        }
    }
}
