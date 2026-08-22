using Aethiumian.AI;
using Aethiumian.AI.Nodes;
using Aethiumian.AI.Variables;
using NUnit.Framework;
using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Serialization;

namespace Aethiumian.AI.Editor.Tests.Variables
{
    public sealed class ArithmeticCompatibilityTests
    {
        [Test]
        public void FloatBroadcastsToVector4AndPreservesW()
        {
            Add node = new()
            {
                a = Constant(VariableType.Float, 2f),
                b = Constant(VariableType.Vector4, new Vector4(1f, 2f, 3f, 4f)),
                result = Reference(VariableType.Vector4, Vector4.zero),
            };

            Assert.That(node.Execute(), Is.EqualTo(State.Success));
            Assert.That(node.result.Vector4Value, Is.EqualTo(new Vector4(3f, 4f, 5f, 6f)));
        }

        [Test]
        public void SameWidthVectorsAreAccepted()
        {
            Min node = new()
            {
                a = Constant(VariableType.Vector3, new Vector3(3f, -2f, 5f)),
                b = Constant(VariableType.Vector3, new Vector3(1f, 4f, 2f)),
                result = Reference(VariableType.Vector3, Vector3.zero),
            };

            Assert.That(node.Execute(), Is.EqualTo(State.Success));
            Assert.That(node.result.Vector3Value, Is.EqualTo(new Vector3(1f, -2f, 2f)));
        }

        [Test]
        public void MinMaxUseDestinationVectorShape()
        {
            Min min = new()
            {
                a = Constant(VariableType.Vector2, new Vector2(3f, 2f)),
                b = Constant(VariableType.Vector4, new Vector4(1f, 4f, 5f, 6f)),
                result = Reference(VariableType.Vector3, Vector3.zero),
            };
            Max max = new()
            {
                a = Constant(VariableType.Vector2, new Vector2(3f, 2f)),
                b = Constant(VariableType.Vector4, new Vector4(1f, 4f, 5f, 6f)),
                result = Reference(VariableType.Vector3, Vector3.zero),
            };

            Assert.That(min.Execute(), Is.EqualTo(State.Success));
            Assert.That(min.result.Vector3Value, Is.EqualTo(new Vector3(1f, 2f, 0f)));
            Assert.That(max.Execute(), Is.EqualTo(State.Success));
            Assert.That(max.result.Vector3Value, Is.EqualTo(new Vector3(3f, 4f, 5f)));
        }

        [Test]
        public void MinMaxPreserveIntegerComparisonAndWriteback()
        {
            Min min = new()
            {
                a = Constant(VariableType.Int, int.MaxValue),
                b = Constant(VariableType.Int, int.MaxValue - 1),
                result = Reference(VariableType.Int, 0),
            };
            Max max = new()
            {
                a = Constant(VariableType.Float, 1.8f),
                b = Constant(VariableType.Float, 1.2f),
                result = Reference(VariableType.Int, 0),
            };

            Assert.That(min.Execute(), Is.EqualTo(State.Success));
            Assert.That(min.result.IntValue, Is.EqualTo(int.MaxValue - 1));
            Assert.That(max.Execute(), Is.EqualTo(State.Success));
            Assert.That(max.result.IntValue, Is.EqualTo(1));
        }

        [Test]
        public void MinMaxRejectInvalidValuesWithoutWriting()
        {
            Min node = new()
            {
                a = Constant(VariableType.Generic, new object()),
                b = Constant(VariableType.Int, 3),
                result = Reference(VariableType.Int, 17),
            };

            Assert.That(node.Execute(), Is.EqualTo(State.Failed));
            Assert.That(node.result.IntValue, Is.EqualTo(17));
        }

        [Test]
        public void MinMaxTreatBoolAsNaturalIntegerComponent()
        {
            Max node = new()
            {
                a = Constant(VariableType.Bool, true),
                b = Constant(VariableType.Int, 0),
                result = Reference(VariableType.Bool, false),
            };

            Assert.That(node.Execute(), Is.EqualTo(State.Success));
            Assert.That(node.result.BoolValue, Is.True);
        }

        [Test]
        public void MinMaxIgnoreTreeArithmeticMode()
        {
            BehaviourTreeData data = ScriptableObject.CreateInstance<BehaviourTreeData>();
            GameObject gameObject = new("MinMaxArithmeticModeTest");
            try
            {
                data.arithmeticMode = ArithmeticMode.Int;
                BehaviourTree tree = new(data, gameObject, null);
                Max node = new()
                {
                    a = Constant(VariableType.Vector2, new Vector2(1f, 4f)),
                    b = Constant(VariableType.Vector4, new Vector4(2f, 3f, 5f, 6f)),
                    result = Reference(VariableType.Vector3, Vector3.zero),
                    behaviourTree = tree,
                };

                Assert.That(node.Execute(), Is.EqualTo(State.Success));
                Assert.That(node.result.Vector3Value, Is.EqualTo(new Vector3(2f, 4f, 5f)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
                UnityEngine.Object.DestroyImmediate(data);
            }
        }

        [Test]
        public void DifferentVectorWidthsNormalizeToDestinationShape()
        {
            Add node = new()
            {
                a = Constant(VariableType.Vector2, new Vector2(1f, 2f)),
                b = Constant(VariableType.Vector4, new Vector4(3f, 4f, 5f, 6f)),
                result = Reference(VariableType.Vector3, Vector3.zero),
            };

            Assert.That(node.Execute(), Is.EqualTo(State.Success));
            Assert.That(node.result.Vector3Value, Is.EqualTo(new Vector3(4f, 6f, 5f)));
        }

        [Test]
        public void VectorValueReturnsCompleteVector4()
        {
            VariableField vector2 = Constant(VariableType.Vector2, new Vector2(1f, 2f));
            VariableField vector3 = Constant(VariableType.Vector3, new Vector3(1f, 2f, 3f));
            VariableField vector4 = Constant(VariableType.Vector4, new Vector4(1f, 2f, 3f, 4f));
            VariableField<object> generic = new();
            generic.ForceSetConstantValue(new Vector4(1f, 2f, 3f, 4f));

            Assert.That(vector2.VectorValue, Is.EqualTo(new Vector4(1f, 2f, 0f, 0f)));
            Assert.That(vector3.VectorValue, Is.EqualTo(new Vector4(1f, 2f, 3f, 0f)));
            Assert.That(vector4.VectorValue, Is.EqualTo(new Vector4(1f, 2f, 3f, 4f)));
            Assert.That(generic.VectorValue, Is.EqualTo(new Vector4(1f, 2f, 3f, 4f)));
        }

        [Test]
        public void CreateVectorNodesSelectLanesFromIndependentSources()
        {
            CreateVector3 create3 = new()
            {
                x = Constant(VariableType.Vector4, new Vector4(1f, 2f, 3f, 4f)),
                xLane = VectorLane.W,
                y = Constant(VariableType.Vector2, new Vector2(5f, 6f)),
                yLane = VectorLane.Y,
                z = Constant(VariableType.Float, 1f),
                vector = TypedReference(VariableType.Vector3, Vector3.zero),
            };
            CreateVector4 create4 = new()
            {
                x = Constant(VariableType.Float, 2f),
                xLane = VectorLane.W,
                y = Constant(VariableType.Vector3, new Vector3(1f, 2f, 3f)),
                yLane = VectorLane.Z,
                z = Constant(VariableType.Vector4, new Vector4(4f, 5f, 6f, 7f)),
                zLane = VectorLane.W,
                w = Constant(VariableType.Float, 0f),
                vector = TypedReference(VariableType.Vector4, Vector4.zero),
            };

            Assert.That(create3.Execute(), Is.EqualTo(State.Success));
            Assert.That(create3.vector.Value, Is.EqualTo(new Vector3(4f, 6f, 1f)));
            Assert.That(create4.Execute(), Is.EqualTo(State.Success));
            Assert.That(create4.vector.Value, Is.EqualTo(new Vector4(2f, 3f, 7f, 0f)));
        }

        [Test]
        public void CreateVectorRejectsMissingSourceLaneWithoutWriting()
        {
            CreateVector2 node = new()
            {
                x = Constant(VariableType.Vector2, new Vector2(1f, 2f)),
                xLane = VectorLane.Z,
                y = Constant(VariableType.Float, 3f),
                vector = TypedReference(VariableType.Vector2, new Vector2(8f, 9f)),
            };

            Assert.That(node.Execute(), Is.EqualTo(State.Failed));
            Assert.That(node.vector.Value, Is.EqualTo(new Vector2(8f, 9f)));
        }

        [Test]
        public void CreateVectorUnsetSourceDefaultsToZero()
        {
            CreateVector2 node = new()
            {
                x = null,
                y = Constant(VariableType.Float, 1f),
                vector = TypedReference(VariableType.Vector2, new Vector2(8f, 9f)),
            };

            Assert.That(node.Execute(), Is.EqualTo(State.Success));
            Assert.That(node.vector.Value, Is.EqualTo(new Vector2(0f, 1f)));
        }

        [Test]
        public void ScalarAndIntScalarValuesProjectAndTruncate()
        {
            VariableField vector = Constant(VariableType.Vector3, new Vector3(1.8f, 2f, 3f));
            VariableField floating = Constant(VariableType.Float, 1.8f);

            Assert.That(vector.ScalarValue, Is.EqualTo(1.8f));
            Assert.That(vector.IntScalarValue, Is.EqualTo(1));
            Assert.That(floating.IntScalarValue, Is.EqualTo(1));
        }

        [Test]
        public void ComponentCountMapsNumericAndVectorShapes()
        {
            Assert.That(VariableType.Int.ComponentCount(), Is.EqualTo(1));
            Assert.That(VariableType.Float.ComponentCount(), Is.EqualTo(1));
            Assert.That(VariableType.Bool.ComponentCount(), Is.EqualTo(1));
            Assert.That(VariableType.Vector2.ComponentCount(), Is.EqualTo(2));
            Assert.That(VariableType.Vector3.ComponentCount(), Is.EqualTo(3));
            Assert.That(VariableType.Vector4.ComponentCount(), Is.EqualTo(4));
            Assert.That(VariableType.String.ComponentCount(), Is.Zero);
            Assert.That(VariableType.UnityObject.ComponentCount(), Is.Zero);
            Assert.That(VariableType.Generic.ComponentCount(), Is.Zero);
            Assert.That(VariableType.Invalid.ComponentCount(), Is.Zero);
            Assert.That(VariableType.Node.ComponentCount(), Is.Zero);
        }

        [Test]
        public void ComponentwiseValueBroadcastsScalarsAndPreservesVectorShape()
        {
            VariableField integer = Constant(VariableType.Int, 2);
            VariableField floating = Constant(VariableType.Float, 1.5f);
            VariableField boolean = Constant(VariableType.Bool, true);
            VariableField vector2 = Constant(VariableType.Vector2, new Vector2(1f, 2f));
            VariableField vector4 = Constant(VariableType.Vector4, new Vector4(1f, 2f, 3f, 4f));

            Assert.That(integer.ComponentwiseValue, Is.EqualTo(new Vector4(2f, 2f, 2f, 2f)));
            Assert.That(floating.ComponentwiseValue, Is.EqualTo(new Vector4(1.5f, 1.5f, 1.5f, 1.5f)));
            Assert.That(boolean.ComponentwiseValue, Is.EqualTo(Vector4.one));
            Assert.That(vector2.ComponentwiseValue, Is.EqualTo(new Vector4(1f, 2f, 0f, 0f)));
            Assert.That(vector4.ComponentwiseValue, Is.EqualTo(new Vector4(1f, 2f, 3f, 4f)));
        }

        [Test]
        public void IntModeNormalizesBoolBroadcastAndNarrowVectorLanes()
        {
            Add node = new()
            {
                a = Constant(VariableType.Vector2, new Vector2(2.9f, -1.8f)),
                b = Constant(VariableType.Bool, true),
                result = Reference(VariableType.Vector3, Vector3.zero),
                operationMode = ArithmeticMode.Int,
            };

            Assert.That(node.Execute(), Is.EqualTo(State.Success));
            Assert.That(node.result.Vector3Value, Is.EqualTo(new Vector3(3f, 0f, 1f)));
        }

        [Test]
        public void IntModeScalarBroadcastPreservesLargeIntegerValue()
        {
            Add node = new()
            {
                a = Constant(VariableType.Int, int.MaxValue),
                b = Constant(VariableType.Int, 0),
                result = Reference(VariableType.Vector2, Vector2.zero),
                operationMode = ArithmeticMode.Int,
            };

            Assert.That(node.Execute(), Is.EqualTo(State.Success));
            Assert.That(node.result.Vector2Value, Is.EqualTo(new Vector2(int.MaxValue, int.MaxValue)));
        }

        [Test]
        public void SetComponentwiseValueUsesDestinationShape()
        {
            VariableReference integer = Reference(VariableType.Int, 0);
            VariableReference floating = Reference(VariableType.Float, 0f);
            VariableReference boolean = Reference(VariableType.Bool, false);
            VariableReference vector2 = Reference(VariableType.Vector2, Vector2.zero);
            VariableReference vector3 = Reference(VariableType.Vector3, Vector3.zero);
            VariableReference vector4 = Reference(VariableType.Vector4, Vector4.zero);
            Vector4 value = new(1.8f, 2f, 3f, 4f);

            integer.SetComponentwiseValue(value);
            floating.SetComponentwiseValue(value);
            boolean.SetComponentwiseValue(value);
            vector2.SetComponentwiseValue(value);
            vector3.SetComponentwiseValue(value);
            vector4.SetComponentwiseValue(value);

            Assert.That(integer.IntValue, Is.EqualTo(1));
            Assert.That(floating.FloatValue, Is.EqualTo(1.8f));
            Assert.That(boolean.BoolValue, Is.True);
            Assert.That(vector2.Vector2Value, Is.EqualTo(new Vector2(1.8f, 2f)));
            Assert.That(vector3.Vector3Value, Is.EqualTo(new Vector3(1.8f, 2f, 3f)));
            Assert.That(vector4.Vector4Value, Is.EqualTo(value));
        }

        [Test]
        public void SetComponentwiseValueRejectsUnsupportedDestinationWithoutWriting()
        {
            VariableReference result = Reference(VariableType.String, "unchanged");

            Assert.Throws<InvalidCastException>(() => result.SetComponentwiseValue(Vector4.zero));
            Assert.That(result.StringValue, Is.EqualTo("unchanged"));
        }

        [Test]
        public void DestinationScalarUsesXAndFloatingPointPrecision()
        {
            Add node = new()
            {
                a = Constant(VariableType.Vector4, new Vector4(1.9f, 8f, 9f, 10f)),
                b = Constant(VariableType.Float, 1.9f),
                result = Reference(VariableType.Int, 0),
                operationMode = ArithmeticMode.Float,
            };

            Assert.That(node.Execute(), Is.EqualTo(State.Success));
            Assert.That(node.result.IntValue, Is.EqualTo(3));
        }

        [Test]
        public void IntModeConvertsOperandsBeforeAddition()
        {
            Add node = new()
            {
                a = Constant(VariableType.Float, 1.8f),
                b = Constant(VariableType.Float, 1.8f),
                result = Reference(VariableType.Int, 0),
                operationMode = ArithmeticMode.Int,
            };

            Assert.That(node.Execute(), Is.EqualTo(State.Success));
            Assert.That(node.result.IntValue, Is.EqualTo(2));
        }

        [Test]
        public void DefaultModeUsesFloatWhenNoTreeIsAttached()
        {
            Add node = new()
            {
                a = Constant(VariableType.Float, 1.8f),
                b = Constant(VariableType.Float, 1.8f),
                result = Reference(VariableType.Int, 0),
            };

            Assert.That(node.operationMode, Is.EqualTo(ArithmeticMode.Default));
            Assert.That(node.Execute(), Is.EqualTo(State.Success));
            Assert.That(node.result.IntValue, Is.EqualTo(3));
        }

        [Test]
        public void TreeDefaultModeFallsBackToFloat()
        {
            BehaviourTreeData data = ScriptableObject.CreateInstance<BehaviourTreeData>();
            GameObject gameObject = new("ArithmeticTreeDefaultModeTest");
            try
            {
                BehaviourTree tree = new(data, gameObject, null);
                Add node = new()
                {
                    a = Constant(VariableType.Float, 1.8f),
                    b = Constant(VariableType.Float, 1.8f),
                    result = Reference(VariableType.Int, 0),
                    behaviourTree = tree,
                };

                Assert.That(data.arithmeticMode, Is.EqualTo(ArithmeticMode.Default));
                Assert.That(node.Execute(), Is.EqualTo(State.Success));
                Assert.That(node.result.IntValue, Is.EqualTo(3));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
                UnityEngine.Object.DestroyImmediate(data);
            }
        }

        [Test]
        public void NodeModeOverridesTreeMode()
        {
            BehaviourTreeData data = ScriptableObject.CreateInstance<BehaviourTreeData>();
            GameObject gameObject = new("ArithmeticModeTest");
            try
            {
                data.arithmeticMode = ArithmeticMode.Int;
                BehaviourTree tree = new(data, gameObject, null);
                Add node = new()
                {
                    a = Constant(VariableType.Float, 1.8f),
                    b = Constant(VariableType.Float, 1.8f),
                    result = Reference(VariableType.Int, 0),
                    operationMode = ArithmeticMode.Float,
                    behaviourTree = tree,
                };

                Assert.That(node.Execute(), Is.EqualTo(State.Success));
                Assert.That(node.result.IntValue, Is.EqualTo(3));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
                UnityEngine.Object.DestroyImmediate(data);
            }
        }

        [Test]
        public void DefaultNodeModeInheritsTreeMode()
        {
            BehaviourTreeData data = ScriptableObject.CreateInstance<BehaviourTreeData>();
            GameObject gameObject = new("ArithmeticModeInheritanceTest");
            try
            {
                data.arithmeticMode = ArithmeticMode.Int;
                BehaviourTree tree = new(data, gameObject, null);
                Add node = new()
                {
                    a = Constant(VariableType.Float, 1.8f),
                    b = Constant(VariableType.Float, 1.8f),
                    result = Reference(VariableType.Int, 0),
                    behaviourTree = tree,
                };

                Assert.That(node.Execute(), Is.EqualTo(State.Success));
                Assert.That(node.result.IntValue, Is.EqualTo(2));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
                UnityEngine.Object.DestroyImmediate(data);
            }
        }

        [Test]
        public void OnlyComponentwiseNodesExposeOperationMode()
        {
            Assert.That(typeof(Add).GetField("operationMode"), Is.Not.Null);
            Assert.That(typeof(Subtract).GetField("operationMode"), Is.Not.Null);
            Assert.That(typeof(Multiply).GetField("operationMode"), Is.Not.Null);
            Assert.That(typeof(Divide).GetField("operationMode"), Is.Not.Null);
            Assert.That(typeof(Min).GetField("operationMode"), Is.Null);
            Assert.That(typeof(Max).GetField("operationMode"), Is.Null);
            Assert.That(typeof(Arctangent2).GetField("operationMode"), Is.Null);
            Assert.That(typeof(Round).GetField("operationMode"), Is.Null);
            Assert.That(typeof(Normalize).GetField("operationMode"), Is.Null);
            Assert.That(typeof(Aethiumian.AI.Nodes.Random).GetField("operationMode"), Is.Null);
            Assert.That(typeof(Compare).GetField("operationMode"), Is.Null);
        }

        [Test]
        public void BinaryNodesShareComponentwiseFieldsAndSerializationAliases()
        {
            FieldInfo a = typeof(ComponentwiseBinaryArithmetic).GetField("a");
            FieldInfo b = typeof(ComponentwiseBinaryArithmetic).GetField("b");
            FieldInfo result = typeof(ComponentwiseBinaryArithmetic).GetField("result");

            Assert.That(a, Is.Not.Null);
            Assert.That(b, Is.Not.Null);
            Assert.That(result, Is.Not.Null);
            Assert.That(typeof(Add).GetField("a"), Is.SameAs(a));
            Assert.That(typeof(Arctangent2).GetField("b"), Is.SameAs(b));
            Assert.That(typeof(Add).GetField("a", BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance), Is.Null);
            Assert.That(typeof(Arctangent2).GetField("result", BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance), Is.Null);
            Assert.That(a.GetCustomAttribute<FormerlySerializedAsAttribute>().oldName, Is.EqualTo("y"));
            Assert.That(b.GetCustomAttribute<FormerlySerializedAsAttribute>().oldName, Is.EqualTo("x"));
        }

        [Test]
        public void FloatDestinationPromotesIntegerDivision()
        {
            Divide node = new()
            {
                a = Constant(VariableType.Int, 5),
                b = Constant(VariableType.Int, 2),
                result = Reference(VariableType.Float, 0f),
            };

            Assert.That(node.Execute(), Is.EqualTo(State.Success));
            Assert.That(node.result.FloatValue, Is.EqualTo(2.5f));
        }

        [Test]
        public void IntModeUsesIntegerDivisionBeforeFloatWriteback()
        {
            Divide node = new()
            {
                a = Constant(VariableType.Int, 5),
                b = Constant(VariableType.Int, 2),
                result = Reference(VariableType.Float, 0f),
                operationMode = ArithmeticMode.Int,
            };

            Assert.That(node.Execute(), Is.EqualTo(State.Success));
            Assert.That(node.result.FloatValue, Is.EqualTo(2f));
        }

        [Test]
        public void IntModeBroadcastsScalarOperandsToVectorResult()
        {
            Add node = new()
            {
                a = Constant(VariableType.Int, 1),
                b = Constant(VariableType.Int, 2),
                result = Reference(VariableType.Vector3, new Vector3(7f, 8f, 9f)),
                operationMode = ArithmeticMode.Int,
            };

            Assert.That(node.Execute(), Is.EqualTo(State.Success));
            Assert.That(node.result.Vector3Value, Is.EqualTo(new Vector3(3f, 3f, 3f)));
        }

        [Test]
        public void SubtractUsesDestinationVectorShape()
        {
            Subtract node = new()
            {
                a = Constant(VariableType.Vector2, new Vector2(10f, 20f)),
                b = Constant(VariableType.Vector4, new Vector4(1f, 2f, 3f, 4f)),
                result = Reference(VariableType.Vector3, Vector3.zero),
            };

            Assert.That(node.Execute(), Is.EqualTo(State.Success));
            Assert.That(node.result.Vector3Value, Is.EqualTo(new Vector3(9f, 18f, -3f)));
        }

        [Test]
        public void MultiplyBroadcastsScalarToDestinationVector()
        {
            Multiply node = new()
            {
                a = Constant(VariableType.Float, 2f),
                b = Constant(VariableType.Vector3, new Vector3(1f, 2f, 3f)),
                result = Reference(VariableType.Vector3, Vector3.zero),
            };

            Assert.That(node.Execute(), Is.EqualTo(State.Success));
            Assert.That(node.result.Vector3Value, Is.EqualTo(new Vector3(2f, 4f, 6f)));
        }

        [Test]
        public void DivideUsesDestinationVectorShape()
        {
            Divide node = new()
            {
                a = Constant(VariableType.Vector2, new Vector2(8f, 9f)),
                b = Constant(VariableType.Vector4, new Vector4(2f, 3f, 1f, 1f)),
                result = Reference(VariableType.Vector3, Vector3.zero),
            };

            Assert.That(node.Execute(), Is.EqualTo(State.Success));
            Assert.That(node.result.Vector3Value, Is.EqualTo(new Vector3(4f, 3f, 0f)));
        }

        [Test]
        public void ArithmeticWritesNonzeroResultToBoolAsTrue()
        {
            Add node = new()
            {
                a = Constant(VariableType.Bool, true),
                b = Constant(VariableType.Bool, true),
                result = Reference(VariableType.Bool, false),
                operationMode = ArithmeticMode.Int,
            };

            Assert.That(node.Execute(), Is.EqualTo(State.Success));
            Assert.That(node.result.BoolValue, Is.True);
        }

        [Test]
        public void FourWayArithmeticRejectsUnsupportedDestination()
        {
            Add node = new()
            {
                a = Constant(VariableType.Int, 2),
                b = Constant(VariableType.Int, 3),
                result = Reference(VariableType.String, "unchanged"),
            };

            Assert.That(node.Execute(), Is.EqualTo(State.Failed));
            Assert.That(node.result.StringValue, Is.EqualTo("unchanged"));
        }

        [Test]
        public void FourWayArithmeticRejectsNonNumericOperandWithoutWriting()
        {
            Add node = new()
            {
                a = Constant(VariableType.Generic, new object()),
                b = Constant(VariableType.Int, 3),
                result = Reference(VariableType.Int, 17),
            };

            Assert.That(node.Execute(), Is.EqualTo(State.Failed));
            Assert.That(node.result.IntValue, Is.EqualTo(17));
        }

        [Test]
        public void IntModeSupportsVectorShapeAndConvertsEachOperandLaneBeforeAddition()
        {
            Add node = new()
            {
                a = Constant(VariableType.Vector2, new Vector2(2.2f, 1.8f)),
                b = Constant(VariableType.Vector2, new Vector2(6.7f, 8.9f)),
                result = Reference(VariableType.Vector2, Vector2.zero),
                operationMode = ArithmeticMode.Int,
            };

            Assert.That(node.Execute(), Is.EqualTo(State.Success));
            Assert.That(node.result.Vector2Value, Is.EqualTo(new Vector2(8f, 9f)));
        }

        [Test]
        public void IntModeSupportsVector3AndVector4AcrossRemainingArithmeticNodes()
        {
            Subtract subtract = new()
            {
                a = Constant(VariableType.Vector3, new Vector3(9.9f, 8.1f, 7.7f)),
                b = Constant(VariableType.Float, 2.8f),
                result = Reference(VariableType.Vector3, Vector3.zero),
                operationMode = ArithmeticMode.Int,
            };
            Multiply multiply = new()
            {
                a = Constant(VariableType.Vector4, new Vector4(1.9f, 2.1f, 3.8f, 4.2f)),
                b = Constant(VariableType.Int, 2),
                result = Reference(VariableType.Vector4, Vector4.zero),
                operationMode = ArithmeticMode.Int,
            };
            Divide divide = new()
            {
                a = Constant(VariableType.Vector3, new Vector3(9.9f, 8.1f, 7.7f)),
                b = Constant(VariableType.Vector3, new Vector3(2.2f, 4.9f, 3.1f)),
                result = Reference(VariableType.Vector3, Vector3.zero),
                operationMode = ArithmeticMode.Int,
            };

            Assert.That(subtract.Execute(), Is.EqualTo(State.Success));
            Assert.That(subtract.result.Vector3Value, Is.EqualTo(new Vector3(7f, 6f, 5f)));
            Assert.That(multiply.Execute(), Is.EqualTo(State.Success));
            Assert.That(multiply.result.Vector4Value, Is.EqualTo(new Vector4(2f, 4f, 6f, 8f)));
            Assert.That(divide.Execute(), Is.EqualTo(State.Success));
            Assert.That(divide.result.Vector3Value, Is.EqualTo(new Vector3(4f, 2f, 2f)));
        }

        [Test]
        public void IntModePreservesLargeScalarIntegerUntilTypedWriteback()
        {
            Add node = new()
            {
                a = Constant(VariableType.Int, int.MaxValue),
                b = Constant(VariableType.Int, 0),
                result = Reference(VariableType.Int, 0),
                operationMode = ArithmeticMode.Int,
            };

            Assert.That(node.Execute(), Is.EqualTo(State.Success));
            Assert.That(node.result.IntValue, Is.EqualTo(int.MaxValue));
        }

        [Test]
        public void IntModeVectorDivisionByZeroFailsWithoutWriting()
        {
            Divide node = new()
            {
                a = Constant(VariableType.Vector3, new Vector3(8f, 9f, 10f)),
                b = Constant(VariableType.Vector3, new Vector3(2f, 0f, 5f)),
                result = Reference(VariableType.Vector3, new Vector3(7f, 8f, 9f)),
                operationMode = ArithmeticMode.Int,
            };

            Assert.That(node.Execute(), Is.EqualTo(State.Failed));
            Assert.That(node.result.Vector3Value, Is.EqualTo(new Vector3(7f, 8f, 9f)));
        }

        [Test]
        public void IntModeVector4DivisionEvaluatesZeroFilledLanes()
        {
            Divide node = new()
            {
                a = Constant(VariableType.Vector4, new Vector4(8f, 9f, 10f, 11f)),
                b = Constant(VariableType.Vector2, new Vector2(2f, 3f)),
                result = Reference(VariableType.Vector4, new Vector4(7f, 8f, 9f, 10f)),
                operationMode = ArithmeticMode.Int,
            };

            Assert.That(node.Execute(), Is.EqualTo(State.Failed));
            Assert.That(node.result.Vector4Value, Is.EqualTo(new Vector4(7f, 8f, 9f, 10f)));
        }

        [Test]
        public void DivideByZeroFailsWithoutWriting()
        {
            Divide node = new()
            {
                a = Constant(VariableType.Int, 5),
                b = Constant(VariableType.Int, 0),
                result = Reference(VariableType.Int, 17),
            };

            Assert.That(node.Execute(), Is.EqualTo(State.Failed));
            Assert.That(node.result.IntValue, Is.EqualTo(17));
        }

        [Test]
        public void StringAddAndMultiplyAreRejectedWithoutWriting()
        {
            Add add = new()
            {
                a = Constant(VariableType.String, "a"),
                b = Constant(VariableType.String, "b"),
                result = Reference(VariableType.String, string.Empty),
            };
            Multiply multiply = new()
            {
                a = Constant(VariableType.String, "ab"),
                b = Constant(VariableType.Int, 2),
                result = Reference(VariableType.String, string.Empty),
            };

            Assert.That(add.Execute(), Is.EqualTo(State.Failed));
            Assert.That(add.result.StringValue, Is.EqualTo(string.Empty));
            Assert.That(multiply.Execute(), Is.EqualTo(State.Failed));
            Assert.That(multiply.result.StringValue, Is.EqualTo(string.Empty));
        }

        [Test]
        public void FourWayArithmeticWarmPathDoesNotAllocate()
        {
            VariableField left = Constant(VariableType.Int, 7);
            VariableField right = Constant(VariableType.Int, 3);
            VariableReference addResult = Reference(VariableType.Int, 0);
            VariableReference subtractResult = Reference(VariableType.Int, 0);
            VariableReference multiplyResult = Reference(VariableType.Int, 0);
            VariableReference divideResult = Reference(VariableType.Int, 1);
            VariableReference minResult = Reference(VariableType.Int, 0);
            VariableReference maxResult = Reference(VariableType.Int, 0);
            Add add = new() { a = left, b = right, result = addResult };
            Subtract subtract = new() { a = left, b = right, result = subtractResult };
            Multiply multiply = new() { a = left, b = right, result = multiplyResult };
            Divide divide = new() { a = left, b = right, result = divideResult };
            Min min = new() { a = left, b = right, result = minResult };
            Max max = new() { a = left, b = right, result = maxResult };

            _ = add.Execute();
            _ = subtract.Execute();
            _ = multiply.Execute();
            _ = divide.Execute();
            _ = min.Execute();
            _ = max.Execute();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++)
            {
                _ = add.Execute();
                _ = subtract.Execute();
                _ = multiply.Execute();
                _ = divide.Execute();
                _ = min.Execute();
                _ = max.Execute();
            }

            Assert.That(GC.GetAllocatedBytesForCurrentThread() - before, Is.EqualTo(0));
            Assert.That(addResult.IntValue, Is.EqualTo(10));
            Assert.That(subtractResult.IntValue, Is.EqualTo(4));
            Assert.That(multiplyResult.IntValue, Is.EqualTo(21));
            Assert.That(divideResult.IntValue, Is.EqualTo(2));
            Assert.That(minResult.IntValue, Is.EqualTo(3));
            Assert.That(maxResult.IntValue, Is.EqualTo(7));
        }

        [Test]
        public void UnaryArithmeticWarmPathDoesNotAllocate()
        {
            VariableField input = Constant(VariableType.Float, 4.25f);
            Absolute absolute = new() { a = input, result = Reference(VariableType.Float, 0f) };
            Ceil ceil = new() { a = input, result = Reference(VariableType.Float, 0f) };
            Floor floor = new() { a = input, result = Reference(VariableType.Float, 0f) };
            Round round = new() { a = input, result = Reference(VariableType.Float, 0f) };
            SquareRoot squareRoot = new() { a = input, result = Reference(VariableType.Float, 0f) };

            _ = absolute.Execute();
            _ = ceil.Execute();
            _ = floor.Execute();
            _ = round.Execute();
            _ = squareRoot.Execute();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++)
            {
                _ = absolute.Execute();
                _ = ceil.Execute();
                _ = floor.Execute();
                _ = round.Execute();
                _ = squareRoot.Execute();
            }

            Assert.That(GC.GetAllocatedBytesForCurrentThread() - before, Is.EqualTo(0));
            Assert.That(absolute.result.FloatValue, Is.EqualTo(4.25f));
            Assert.That(ceil.result.FloatValue, Is.EqualTo(5f));
            Assert.That(floor.result.FloatValue, Is.EqualTo(4f));
            Assert.That(round.result.FloatValue, Is.EqualTo(4f));
            Assert.That(squareRoot.result.FloatValue, Is.EqualTo(Mathf.Sqrt(4.25f)).Within(0.0001f));
        }

        [Test]
        public void IntIntArithmeticKeepsIntegerResult()
        {
            Multiply node = new()
            {
                a = Constant(VariableType.Int, 7),
                b = Constant(VariableType.Int, 6),
                result = Reference(VariableType.Int, 0),
            };

            Assert.That(node.Execute(), Is.EqualTo(State.Success));
            Assert.That(node.result.IntValue, Is.EqualTo(42));
        }

        [Test]
        public void DivideUsesVector2Components()
        {
            Divide node = new()
            {
                a = Constant(VariableType.Vector2, new Vector2(8f, 9f)),
                b = Constant(VariableType.Float, 2f),
                result = Reference(VariableType.Vector2, Vector2.zero),
            };

            Assert.That(node.Execute(), Is.EqualTo(State.Success));
            Assert.That(node.result.Vector2Value, Is.EqualTo(new Vector2(4f, 4.5f)));
        }

        [Test]
        public void MinMaxAndRoundingSupportVector4()
        {
            Max max = new()
            {
                a = Constant(VariableType.Vector4, new Vector4(1.2f, -2.2f, 3.9f, 4.1f)),
                b = Constant(VariableType.Float, 2f),
                result = Reference(VariableType.Vector4, Vector4.zero),
            };
            Floor floor = new()
            {
                a = Constant(VariableType.Vector4, new Vector4(1.9f, -2.1f, 3.5f, 4.9f)),
                result = Reference(VariableType.Vector4, Vector4.zero),
            };
            Ceil ceil = new()
            {
                a = Constant(VariableType.Vector4, new Vector4(1.1f, -2.9f, 3.1f, 4.01f)),
                result = Reference(VariableType.Vector4, Vector4.zero),
            };
            Round round = new()
            {
                a = Constant(VariableType.Vector4, new Vector4(1.4f, 2.6f, -3.4f, -4.6f)),
                result = Reference(VariableType.Vector4, Vector4.zero),
            };

            Assert.That(max.Execute(), Is.EqualTo(State.Success));
            Assert.That(max.result.Vector4Value, Is.EqualTo(new Vector4(2f, 2f, 3.9f, 4.1f)));
            Assert.That(floor.Execute(), Is.EqualTo(State.Success));
            Assert.That(floor.result.Vector4Value, Is.EqualTo(new Vector4(1f, -3f, 3f, 4f)));
            Assert.That(ceil.Execute(), Is.EqualTo(State.Success));
            Assert.That(ceil.result.Vector4Value, Is.EqualTo(new Vector4(2f, -2f, 4f, 5f)));
            Assert.That(round.Execute(), Is.EqualTo(State.Success));
            Assert.That(round.result.Vector4Value, Is.EqualTo(new Vector4(1f, 3f, -3f, -5f)));
        }

        [Test]
        public void UnaryNodesDispatchConstantsAndVariableReferencesByDestinationShape()
        {
            Absolute absolute = new()
            {
                a = FieldReference(VariableType.Float, -1.5f),
                result = Reference(VariableType.Vector3, Vector3.zero),
            };
            Ceil ceil = new()
            {
                a = Constant(VariableType.Vector2, new Vector2(1.2f, -1.8f)),
                result = Reference(VariableType.Vector4, Vector4.zero),
            };
            Floor floor = new()
            {
                a = Constant(VariableType.Vector4, new Vector4(1.9f, -2.1f, 3.5f, 4.9f)),
                result = Reference(VariableType.Int, 0),
            };
            Round round = new()
            {
                a = Constant(VariableType.Float, 2.6f),
                result = Reference(VariableType.Vector2, Vector2.zero),
            };

            Assert.That(absolute.Execute(), Is.EqualTo(State.Success));
            Assert.That(absolute.result.Vector3Value, Is.EqualTo(new Vector3(1.5f, 1.5f, 1.5f)));
            Assert.That(ceil.Execute(), Is.EqualTo(State.Success));
            Assert.That(ceil.result.Vector4Value, Is.EqualTo(new Vector4(2f, -1f, 0f, 0f)));
            Assert.That(floor.Execute(), Is.EqualTo(State.Success));
            Assert.That(floor.result.IntValue, Is.EqualTo(1));
            Assert.That(round.Execute(), Is.EqualTo(State.Success));
            Assert.That(round.result.Vector2Value, Is.EqualTo(new Vector2(3f, 3f)));
        }

        [Test]
        public void UnaryIntegerDispatchSupportsBoolAndPreservesIntPrecision()
        {
            Absolute boolean = new()
            {
                a = Constant(VariableType.Bool, true),
                result = Reference(VariableType.Bool, false),
            };
            Absolute minimum = new()
            {
                a = Constant(VariableType.Int, int.MinValue),
                result = Reference(VariableType.Int, 0),
            };

            Assert.That(boolean.Execute(), Is.EqualTo(State.Success));
            Assert.That(boolean.result.BoolValue, Is.True);
            Assert.That(minimum.Execute(), Is.EqualTo(State.Success));
            Assert.That(minimum.result.IntValue, Is.EqualTo(int.MinValue));
        }

        [Test]
        public void SquareRootUsesFloatingPointSemanticsForIntegerInput()
        {
            SquareRoot node = new()
            {
                a = Constant(VariableType.Int, 2),
                result = Reference(VariableType.Float, 0f),
            };

            Assert.That(node.Execute(), Is.EqualTo(State.Success));
            Assert.That(node.result.FloatValue, Is.EqualTo(Mathf.Sqrt(2f)).Within(0.0001f));
        }

        [Test]
        public void SquareRootWritesNaNByDefaultAndCanFailBeforeWrite()
        {
            SquareRoot defaultNode = new()
            {
                a = Constant(VariableType.Float, -1f),
                result = Reference(VariableType.Float, 42f),
            };
            SquareRoot failingNode = new()
            {
                a = Constant(VariableType.Float, -1f),
                result = Reference(VariableType.Float, 42f),
                failOnNaN = true,
            };
            SquareRoot intTarget = new()
            {
                a = Constant(VariableType.Float, -1f),
                result = Reference(VariableType.Int, 17),
            };

            Assert.That(defaultNode.Execute(), Is.EqualTo(State.Success));
            Assert.That(float.IsNaN(defaultNode.result.FloatValue), Is.True);
            Assert.That(failingNode.Execute(), Is.EqualTo(State.Failed));
            Assert.That(failingNode.result.FloatValue, Is.EqualTo(42f));
            Assert.That(intTarget.Execute(), Is.EqualTo(State.Failed));
            Assert.That(intTarget.result.IntValue, Is.EqualTo(17));
        }

        [Test]
        public void SquareRootIgnoresNegativeInactiveLanes()
        {
            SquareRoot node = new()
            {
                a = Constant(VariableType.Vector3, new Vector3(4f, 9f, -1f)),
                result = Reference(VariableType.Float, 0f),
            };

            Assert.That(node.Execute(), Is.EqualTo(State.Success));
            Assert.That(node.result.FloatValue, Is.EqualTo(2f));
        }

        [Test]
        public void ArithmeticNodesShareTheSerializedNaNPolicy()
        {
            var baseType = typeof(ComponentwiseUnaryArithmetic);
            Assert.That(baseType.GetField("a")?.FieldType, Is.EqualTo(typeof(VariableField)));
            Assert.That(baseType.GetField("result")?.FieldType, Is.EqualTo(typeof(VariableReference)));
            Assert.That(typeof(Arithmetic).GetField("failOnNaN", BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly), Is.Not.Null);
            Assert.That(typeof(Arithmetic).GetField("failOnNaN")?.GetValue(new Add()), Is.EqualTo(false));
            Assert.That(typeof(SquareRoot).GetField("failOnNaN", BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly), Is.Null);
            Assert.That(typeof(Absolute).GetField("failOnNaN", BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly), Is.Null);
            Assert.That(typeof(Ceil).GetField("failOnNaN", BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly), Is.Null);
            Assert.That(typeof(Floor).GetField("failOnNaN", BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly), Is.Null);
            Assert.That(typeof(Round).GetField("failOnNaN", BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly), Is.Null);

            ComponentwiseInt4 value = new(-2, 3, -4, 5);
            Assert.That(ComponentwiseInt4.Abs(value).x, Is.EqualTo(2));
            Assert.That(ComponentwiseInt4.Abs(value).z, Is.EqualTo(4));
        }

        [Test]
        public void FailOnNaNRejectsActiveInputWithoutWriting()
        {
            Add active = new()
            {
                a = Constant(VariableType.Vector2, new Vector2(float.NaN, 2f)),
                b = Constant(VariableType.Float, 1f),
                result = Reference(VariableType.Vector2, new Vector2(7f, 8f)),
                failOnNaN = true,
            };
            Add inactive = new()
            {
                a = Constant(VariableType.Vector4, new Vector4(1f, 2f, 3f, float.NaN)),
                b = Constant(VariableType.Float, 1f),
                result = Reference(VariableType.Vector2, new Vector2(7f, 8f)),
                failOnNaN = true,
            };

            Assert.That(active.Execute(), Is.EqualTo(State.Failed));
            Assert.That(active.result.Vector2Value, Is.EqualTo(new Vector2(7f, 8f)));
            Assert.That(inactive.Execute(), Is.EqualTo(State.Success));
            Assert.That(inactive.result.Vector2Value, Is.EqualTo(new Vector2(2f, 3f)));
        }

        [Test]
        public void FailOnNaNAppliesToScalarUnaryAndPreservesDefaultPropagation()
        {
            Sine failing = new()
            {
                a = Constant(VariableType.Float, float.NaN),
                result = Reference(VariableType.Float, 9f),
                failOnNaN = true,
            };
            Sine propagating = new()
            {
                a = Constant(VariableType.Float, float.NaN),
                result = Reference(VariableType.Float, 9f),
            };

            Assert.That(failing.Execute(), Is.EqualTo(State.Failed));
            Assert.That(failing.result.FloatValue, Is.EqualTo(9f));
            Assert.That(propagating.Execute(), Is.EqualTo(State.Success));
            Assert.That(float.IsNaN(propagating.result.FloatValue), Is.True);
        }

        [Test]
        public void TrigonometricUnaryNodesUseDestinationVectorShape()
        {
            Sine sine = new()
            {
                a = Constant(VariableType.Vector2, new Vector2(0f, Mathf.PI / 2f)),
                result = Reference(VariableType.Vector3, Vector3.zero),
            };
            Cosine cosine = new()
            {
                a = Constant(VariableType.Vector2, new Vector2(0f, Mathf.PI)),
                result = Reference(VariableType.Vector3, Vector3.zero),
            };
            Tangent tangent = new()
            {
                a = Constant(VariableType.Vector2, new Vector2(0f, Mathf.PI / 4f)),
                result = Reference(VariableType.Vector3, Vector3.zero),
            };
            Arctangent arctangent = new()
            {
                a = Constant(VariableType.Vector2, new Vector2(0f, 1f)),
                result = Reference(VariableType.Vector3, Vector3.zero),
            };

            Assert.That(sine.Execute(), Is.EqualTo(State.Success));
            Assert.That(sine.result.Vector3Value.x, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(sine.result.Vector3Value.y, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(sine.result.Vector3Value.z, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(cosine.Execute(), Is.EqualTo(State.Success));
            Assert.That(cosine.result.Vector3Value.x, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(cosine.result.Vector3Value.y, Is.EqualTo(-1f).Within(0.0001f));
            Assert.That(cosine.result.Vector3Value.z, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(tangent.Execute(), Is.EqualTo(State.Success));
            Assert.That(tangent.result.Vector3Value.x, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(tangent.result.Vector3Value.y, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(tangent.result.Vector3Value.z, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(arctangent.Execute(), Is.EqualTo(State.Success));
            Assert.That(arctangent.result.Vector3Value.x, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(arctangent.result.Vector3Value.y, Is.EqualTo(Mathf.PI / 4f).Within(0.0001f));
            Assert.That(arctangent.result.Vector3Value.z, Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void InverseTrigonometricNodesValidateActiveVectorLanes()
        {
            Arcsine arcsine = new()
            {
                a = Constant(VariableType.Vector2, new Vector2(0f, 1f)),
                result = Reference(VariableType.Vector3, Vector3.zero),
            };
            Arccosine arccosine = new()
            {
                a = Constant(VariableType.Vector4, new Vector4(1f, 0f, 2f, float.NaN)),
                result = Reference(VariableType.Vector2, Vector2.zero),
                failOnNaN = true,
            };
            Arcsine invalid = new()
            {
                a = Constant(VariableType.Vector3, new Vector3(0f, 2f, 0f)),
                result = Reference(VariableType.Vector3, new Vector3(7f, 8f, 9f)),
            };

            Assert.That(arcsine.Execute(), Is.EqualTo(State.Success));
            Assert.That(arcsine.result.Vector3Value.x, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(arcsine.result.Vector3Value.y, Is.EqualTo(Mathf.PI / 2f).Within(0.0001f));
            Assert.That(arcsine.result.Vector3Value.z, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(arccosine.Execute(), Is.EqualTo(State.Success));
            Assert.That(arccosine.result.Vector2Value.x, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(arccosine.result.Vector2Value.y, Is.EqualTo(Mathf.PI / 2f).Within(0.0001f));
            Assert.That(invalid.Execute(), Is.EqualTo(State.Failed));
            Assert.That(invalid.result.Vector3Value, Is.EqualTo(new Vector3(7f, 8f, 9f)));
        }

        [Test]
        public void Arctangent2UsesComponentwiseFloatDispatch()
        {
            Arctangent2 node = new()
            {
                a = Constant(VariableType.Vector2, new Vector2(1f, 0f)),
                b = Constant(VariableType.Vector4, new Vector4(0f, 1f, 5f, 0f)),
                result = Reference(VariableType.Vector3, Vector3.zero),
            };
            Arctangent2 inactiveZeroPair = new()
            {
                a = Constant(VariableType.Vector3, new Vector3(1f, 2f, 0f)),
                b = Constant(VariableType.Vector3, new Vector3(1f, 1f, 0f)),
                result = Reference(VariableType.Vector2, Vector2.zero),
            };
            Arctangent2 activeZeroPair = new()
            {
                a = Constant(VariableType.Vector3, new Vector3(1f, 0f, 0f)),
                b = Constant(VariableType.Vector3, new Vector3(1f, 0f, 1f)),
                result = Reference(VariableType.Vector3, new Vector3(7f, 8f, 9f)),
            };

            Assert.That(node.Execute(), Is.EqualTo(State.Success));
            Assert.That(node.result.Vector3Value.x, Is.EqualTo(Mathf.PI / 2f).Within(0.0001f));
            Assert.That(node.result.Vector3Value.y, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(node.result.Vector3Value.z, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(inactiveZeroPair.Execute(), Is.EqualTo(State.Success));
            Assert.That(inactiveZeroPair.result.Vector2Value.x, Is.EqualTo(Mathf.PI / 4f).Within(0.0001f));
            Assert.That(inactiveZeroPair.result.Vector2Value.y, Is.EqualTo(Mathf.Atan(2f)).Within(0.0001f));
            Assert.That(activeZeroPair.Execute(), Is.EqualTo(State.Failed));
            Assert.That(activeZeroPair.result.Vector3Value, Is.EqualTo(new Vector3(7f, 8f, 9f)));
        }

        [Test]
        public void TrigonometricNodesUseFloatSemanticsForIntegerInput()
        {
            Sine node = new()
            {
                a = Constant(VariableType.Int, 2),
                result = Reference(VariableType.Float, 0f),
            };

            Assert.That(node.Execute(), Is.EqualTo(State.Success));
            Assert.That(node.result.FloatValue, Is.EqualTo(Mathf.Sin(2f)).Within(0.0001f));
        }

        [Test]
        public void GenericTypeDoesNotEnterTypedMathPath()
        {
            Assert.That(
                ArithmeticCompatibility.TryResolveComponentwiseType(
                    VariableType.Generic,
                    VariableType.Float,
                    out _),
                Is.False);
        }

        [Test]
        public void TypedScalarReadsDoNotAllocateAfterWarmup()
        {
            VariableField<float> field = 2.5f;
            _ = field.GetValue<float>();

            long before = GC.GetAllocatedBytesForCurrentThread();
            float sink = 0f;
            for (int i = 0; i < 1000; i++)
            {
                sink += field.GetValue<float>();
            }

            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(sink, Is.EqualTo(2500f));
            Assert.That(allocated, Is.EqualTo(0));
        }

        [Test]
        public void AssignReadsUsingTheDestinationType()
        {
            VariableReference destination = Reference(VariableType.Float, 0f);
            Assign node = new()
            {
                destination = destination,
                source = Constant(VariableType.Int, 7),
            };

            Assert.That(node.Execute(), Is.EqualTo(State.Success));
            Assert.That(destination.FloatValue, Is.EqualTo(7f));
        }

        [Test]
        public void CopyDispatchesOnTheSourceTypeAndUsesTargetConversion()
        {
            VariableReference destination = Reference(VariableType.Float, 0f);
            Copy node = new()
            {
                from = Constant(VariableType.Int, 7),
                to = destination,
            };

            Assert.That(node.Execute(), Is.EqualTo(State.Success));
            Assert.That(destination.FloatValue, Is.EqualTo(7f));
        }

        private static VariableField Constant(VariableType type, object value)
        {
            VariableField field = new(type);
            field.ForceSetConstantValue(value);
            return field;
        }

        private static VariableField FieldReference<T>(VariableType type, T value)
        {
            VariableData data = new("Arithmetic input", type);
            TreeVariable variable = new(data);
            variable.SetValue(value);

            VariableField field = new(type);
            field.SetRuntimeReference(variable);
            return field;
        }

        private static VariableReference Reference<T>(VariableType type, T value)
        {
            VariableData data = new("Arithmetic result", type);
            TreeVariable variable = new(data);
            variable.SetValue(value);

            VariableReference reference = new();
            reference.SetRuntimeReference(variable);
            return reference;
        }

        private static VariableReference<T> TypedReference<T>(VariableType type, T value)
        {
            VariableData data = new("Arithmetic typed result", type);
            TreeVariable variable = new(data);
            variable.SetValue(value);

            VariableReference<T> reference = new();
            reference.SetRuntimeReference(variable);
            return reference;
        }
    }
}
