#nullable enable
using Amlos.AI.Accessors;
using Amlos.AI.Nodes;
using Amlos.AI.References;
using Amlos.AI.Variables;
using Minerva.Module;
using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Amlos.AI.Tests
{
    public sealed class NodePropertyAccessorCloneSemanticTests
    {
        private static readonly HashSet<Type> ExcludedNodeTypes = new()
        {
            // Add explicit exclusions here only when a concrete runtime node is intentionally not source-generated.
        };

        [TestCaseSource(nameof(GeneratedCloneCases))]
        public void GeneratedClone_AllGeneratedNodeTypes_ClonesEquivalentIndependentGraph(Type nodeType)
        {
            Assert.That(
                GeneratedNodePropertyAccessorProvider.TryGet(nodeType, out NodePropertyAccessor accessor),
                Is.True,
                $"{nodeType.FullName} should be covered by generated accessors.");

            TreeNode source = CreateNodeSample(nodeType);
            TreeNode clone = accessor.Duplicate(source, DuplicateMode.DeepClone);

            Assert.That(clone, Is.TypeOf(nodeType));
            AssertCloneGraph(source, clone, nodeType.Name);
            AssertMutationIndependent(source, clone, nodeType.Name);
        }

        [Test]
        public void GeneratedAccessorCoverage_AllConcreteRuntimeNodesHaveGeneratedAccessor()
        {
            Type[] missing = GetConcreteRuntimeNodeTypes()
                .Where(type => !ExcludedNodeTypes.Contains(type))
                .Where(type => !GeneratedNodePropertyAccessorProvider.TryGet(type, out _))
                .ToArray();

            Assert.That(
                missing,
                Is.Empty,
                "Concrete runtime node types missing generated accessors: " +
                string.Join(", ", missing.Select(type => type.FullName)));
        }

        [Test]
        public void GetObjectValue_GeneratedClone_PreservesGenericTypeReferenceBaseType()
        {
            GetObjectValue source = new()
            {
                name = "get object value",
                uuid = UUID.NewUUID(),
                parent = new NodeReference(UUID.NewUUID()),
                @object = CreateVariableReference(VariableType.UnityObject),
                type = CreateGenericTypeReference(),
                fieldPointers = new List<FieldPointer>
                {
                    new() { name = "health", data = CreateVariableReference(VariableType.Int) },
                    new() { name = "speed", data = CreateVariableReference(VariableType.Float) },
                },
            };

            GetObjectValue clone = CloneGenerated<GetObjectValue>(source);

            Assert.That(clone.type, Is.Not.SameAs(source.type));
            Assert.That(clone.type.ReferType, Is.EqualTo(typeof(Transform)));
            Assert.That(clone.type.BaseType, Is.EqualTo(typeof(Component)));

            clone.type.SetBaseType(typeof(UnityEngine.Object));

            Assert.That(source.type.BaseType, Is.EqualTo(typeof(Component)));
            Assert.That(clone.type.BaseType, Is.EqualTo(typeof(UnityEngine.Object)));
        }

        [Test]
        public void SetObjectValue_GeneratedClone_DeepClonesFieldChangeDataParameter()
        {
            SetObjectValue source = new()
            {
                name = "set object value",
                uuid = UUID.NewUUID(),
                parent = new NodeReference(UUID.NewUUID()),
                @object = CreateVariableReference(VariableType.UnityObject),
                type = CreateComponentTypeReference(),
                fieldData = new List<FieldChangeData>
                {
                    new() { name = "health", data = new Parameter(VariableType.Int) },
                    new() { name = "speed", data = new Parameter(VariableType.Float) },
                },
            };

            SetObjectValue clone = CloneGenerated<SetObjectValue>(source);

            Assert.That(clone.fieldData, Is.Not.SameAs(source.fieldData));
            Assert.That(clone.fieldData.Count, Is.EqualTo(source.fieldData.Count));
            Assert.That(clone.fieldData[0], Is.Not.SameAs(source.fieldData[0]));
            Assert.That(clone.fieldData[0].data, Is.Not.SameAs(source.fieldData[0].data));
            Assert.That(clone.fieldData[0].data.Type, Is.EqualTo(source.fieldData[0].data.Type));

            UUID sourceParameterId = source.fieldData[0].data.UUID;
            clone.fieldData[0].data.SetReference(new VariableData("other", VariableType.Int));

            Assert.That(source.fieldData[0].data.UUID, Is.EqualTo(sourceParameterId));
            Assert.That(clone.fieldData[0].data.UUID, Is.Not.EqualTo(sourceParameterId));
        }

        public static IEnumerable<TestCaseData> GeneratedCloneCases()
        {
            foreach (Type nodeType in GetConcreteRuntimeNodeTypes().Where(type => !ExcludedNodeTypes.Contains(type)))
            {
                yield return new TestCaseData(nodeType)
                    .SetName($"GeneratedClone_AllGeneratedNodeTypes_ClonesEquivalentIndependentGraph({nodeType.Name})");
            }
        }

        private static IEnumerable<Type> GetConcreteRuntimeNodeTypes()
        {
            return TypeCache.GetTypesDerivedFrom<TreeNode>()
                .Where(type => type.Assembly == typeof(TreeNode).Assembly)
                .Where(type => !type.IsAbstract)
                .Where(type => !type.IsGenericTypeDefinition)
                .Where(IsAccessibleRuntimeNodeType)
                .Where(type => type.GetConstructor(Type.EmptyTypes) != null)
                .OrderBy(type => type.FullName);
        }

        private static bool IsAccessibleRuntimeNodeType(Type type)
        {
            if (type.IsNested)
            {
                return type.IsNestedPublic || type.IsNestedAssembly || type.IsNestedFamORAssem;
            }

            return type.IsPublic || type.IsNotPublic;
        }

        private static T CloneGenerated<T>(T source) where T : TreeNode
        {
            if (!GeneratedNodePropertyAccessorProvider.TryGet(typeof(T), out NodePropertyAccessor accessor))
            {
                throw new AssertionException($"{typeof(T).Name} should be covered by generated accessors.");
            }

            return (T)accessor.Duplicate(source, DuplicateMode.DeepClone);
        }

        private static TreeNode CreateNodeSample(Type nodeType)
        {
            TreeNode node = (TreeNode)(Activator.CreateInstance(nodeType)
                ?? throw new AssertionException($"Cannot create node sample for {nodeType.FullName}."));
            node.name = "sample " + nodeType.Name;
            node.uuid = UUID.NewUUID();
            node.parent = new NodeReference(UUID.NewUUID());
            node.services = new List<NodeReference>
            {
                new(UUID.NewUUID()),
                new(UUID.NewUUID()),
            };

            foreach (FieldInfo field in GetCloneFields(nodeType))
            {
                if (field.DeclaringType == typeof(TreeNodeBase) || field.DeclaringType == typeof(TreeNode))
                {
                    continue;
                }

                object value = CreateSampleValue(field.FieldType, field.Name);
                field.SetValue(node, value);
            }

            return node;
        }

        private static IEnumerable<FieldInfo> GetCloneFields(Type type)
        {
            return type.GetFields(BindingFlags.Instance | BindingFlags.Public)
                .Where(field => !field.IsInitOnly)
                .OrderBy(field => field.DeclaringType?.FullName)
                .ThenBy(field => field.Name);
        }

        private static object CreateSampleValue(Type type, string fieldName)
        {
            if (type == typeof(string)) return "sample-" + fieldName;
            if (type == typeof(bool)) return true;
            if (type == typeof(byte)) return (byte)7;
            if (type == typeof(sbyte)) return (sbyte)7;
            if (type == typeof(short)) return (short)7;
            if (type == typeof(ushort)) return (ushort)7;
            if (type == typeof(int)) return 7;
            if (type == typeof(uint)) return 7u;
            if (type == typeof(long)) return 7L;
            if (type == typeof(ulong)) return 7UL;
            if (type == typeof(float)) return 1.25f;
            if (type == typeof(double)) return 1.25d;
            if (type == typeof(decimal)) return 1.25m;
            if (type == typeof(Vector2)) return new Vector2(1.25f, 2.5f);
            if (type == typeof(Vector3)) return new Vector3(1.25f, 2.5f, 3.75f);
            if (type == typeof(Vector4)) return new Vector4(1.25f, 2.5f, 3.75f, 5f);
            if (type == typeof(Color)) return new Color(0.25f, 0.5f, 0.75f, 1f);
            if (type == typeof(Quaternion)) return Quaternion.Euler(10f, 20f, 30f);
            if (type == typeof(LayerMask)) return (LayerMask)3;
            if (type == typeof(UUID)) return UUID.NewUUID();
            if (type == typeof(Type)) return typeof(Transform);
            if (type.IsEnum) return CreateEnumSample(type);

            if (type == typeof(NodeReference)) return new NodeReference(UUID.NewUUID());
            if (type == typeof(RawNodeReference)) return new RawNodeReference { UUID = UUID.NewUUID() };
            if (type == typeof(Parameter)) return new Parameter(VariableType.Int);
            if (typeof(VariableReferenceBase).IsAssignableFrom(type)) return CreateVariableReferenceFor(type);
            if (typeof(VariableBase).IsAssignableFrom(type)) return CreateVariableBaseFor(type);
            if (typeof(TypeReference).IsAssignableFrom(type)) return CreateTypeReferenceFor(type);
            if (type == typeof(FieldPointer)) return new FieldPointer { name = "sample-field", data = CreateVariableReference(VariableType.Int) };
            if (type == typeof(FieldChangeData)) return new FieldChangeData { name = "sample-field", data = new Parameter(VariableType.Int) };
            if (type == typeof(FunctionReference)) return CreateFunctionReference();

            if (type.IsArray)
            {
                Type elementType = type.GetElementType()
                    ?? throw new AssertionException($"Array type '{type.FullName}' has no element type.");
                Array array = Array.CreateInstance(elementType, 2);
                array.SetValue(CreateSampleValue(elementType, fieldName + "0"), 0);
                array.SetValue(CreateSampleValue(elementType, fieldName + "1"), 1);
                return array;
            }

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                Type elementType = type.GetGenericArguments()[0];
                IList list = (IList)(Activator.CreateInstance(type)
                    ?? throw new AssertionException($"Cannot create list sample for {type.FullName}."));
                list.Add(CreateSampleValue(elementType, fieldName + "0"));
                list.Add(CreateSampleValue(elementType, fieldName + "1"));
                return list;
            }

            if (typeof(UnityEngine.Object).IsAssignableFrom(type))
            {
                return null!;
            }

            if (type.GetConstructor(Type.EmptyTypes) != null)
            {
                object instance = Activator.CreateInstance(type)
                    ?? throw new AssertionException($"Cannot create sample for {type.FullName}.");
                foreach (FieldInfo field in GetCloneFields(type))
                {
                    field.SetValue(instance, CreateSampleValue(field.FieldType, field.Name));
                }

                return instance;
            }

            if (type.IsValueType)
            {
                return Activator.CreateInstance(type);
            }

            throw new NotSupportedException($"No sample value builder for field '{fieldName}' of type '{type.FullName}'.");
        }

        private static object CreateEnumSample(Type type)
        {
            Array values = Enum.GetValues(type);
            return (values.Length > 1 ? values.GetValue(1) : values.GetValue(0))
                ?? throw new AssertionException($"Enum type '{type.FullName}' has no sample value.");
        }

        private static VariableReference CreateVariableReference(VariableType type)
        {
            VariableReference reference = new();
            reference.SetReference(new VariableData(type + " variable", type));
            return reference;
        }

        private static object CreateVariableReferenceFor(Type type)
        {
            VariableReferenceBase reference = (VariableReferenceBase)(Activator.CreateInstance(type)
                ?? throw new AssertionException($"Cannot create variable reference sample for {type.FullName}."));
            if (reference is VariableReference untyped)
            {
                untyped.SetReference(new VariableData("sample variable", VariableType.Int));
            }
            else
            {
                reference.SetReference(new VariableData("sample variable", reference.Type));
            }

            return reference;
        }

        private static object CreateVariableBaseFor(Type type)
        {
            VariableBase field = (VariableBase)(Activator.CreateInstance(type)
                ?? throw new AssertionException($"Cannot create variable holder sample for {type.FullName}."));
            if (field is IGenericVariable || field.Type == VariableType.Invalid || field.Type == VariableType.Generic)
            {
                field.SetReference(new VariableData("sample variable", VariableType.Int));
            }
            else
            {
                field.SetReference(new VariableData("sample variable", field.Type));
            }

            return field;
        }

        private static GenericTypeReference CreateGenericTypeReference()
        {
            GenericTypeReference type = new();
            type.SetBaseType(typeof(Component));
            type.SetReferType(typeof(Transform));
            return type;
        }

        private static TypeReference<Component> CreateComponentTypeReference()
        {
            TypeReference<Component> type = new();
            type.SetReferType(typeof(Transform));
            return type;
        }

        private static object CreateTypeReferenceFor(Type type)
        {
            if (type == typeof(GenericTypeReference))
            {
                return CreateGenericTypeReference();
            }

            TypeReference reference = (TypeReference)(Activator.CreateInstance(type)
                ?? throw new AssertionException($"Cannot create type reference sample for {type.FullName}."));
            Type referType = typeof(Component).IsAssignableFrom(reference.BaseType) ? typeof(Transform) : reference.BaseType;
            reference.SetReferType(referType);
            return reference;
        }

        private static FunctionReference CreateFunctionReference()
        {
            FunctionReference reference = new();
            reference.SetMethod(typeof(string).GetMethod(nameof(string.StartsWith), new[] { typeof(string) }));
            reference.targetObject = CreateVariableReference(VariableType.String);
            return reference;
        }

        private static void AssertCloneGraph(object source, object clone, string path)
        {
            if (source == null || clone == null)
            {
                Assert.That(clone, Is.SameAs(source), path);
                return;
            }

            Type type = source.GetType();
            Assert.That(clone, Is.TypeOf(type), path);

            if (IsAtomic(type) || source is UnityEngine.Object)
            {
                Assert.That(clone, Is.EqualTo(source), path);
                return;
            }

            if (!type.IsValueType)
            {
                Assert.That(clone, Is.Not.SameAs(source), path);
            }

            if (source is TypeReference sourceTypeReference && clone is TypeReference cloneTypeReference)
            {
                AssertTypeReferenceClone(sourceTypeReference, cloneTypeReference, path);
                return;
            }

            if (source is NodeReference sourceNodeReference && clone is NodeReference cloneNodeReference)
            {
                Assert.That(cloneNodeReference.UUID, Is.EqualTo(sourceNodeReference.UUID), path + ".UUID");
                return;
            }

            if (source is RawNodeReference sourceRawNodeReference && clone is RawNodeReference cloneRawNodeReference)
            {
                Assert.That(cloneRawNodeReference.UUID, Is.EqualTo(sourceRawNodeReference.UUID), path + ".UUID");
                return;
            }

            if (source is VariableBase sourceVariable && clone is VariableBase cloneVariable)
            {
                Assert.That(cloneVariable.UUID, Is.EqualTo(sourceVariable.UUID), path + ".UUID");
                Assert.That(cloneVariable.Type, Is.EqualTo(sourceVariable.Type), path + ".Type");
                return;
            }

            if (type.IsArray)
            {
                AssertArrayClone((Array)source, (Array)clone, path);
                return;
            }

            if (source is IList sourceList && clone is IList cloneList)
            {
                AssertListClone(sourceList, cloneList, path);
                return;
            }

            foreach (FieldInfo field in GetCloneFields(type))
            {
                AssertCloneGraph(field.GetValue(source), field.GetValue(clone), path + "." + field.Name);
            }
        }

        private static void AssertArrayClone(Array source, Array clone, string path)
        {
            Assert.That(clone, Is.Not.SameAs(source), path);
            Assert.That(clone.Length, Is.EqualTo(source.Length), path + ".Length");
            for (int i = 0; i < source.Length; i++)
            {
                AssertCloneGraph(source.GetValue(i), clone.GetValue(i), $"{path}[{i}]");
            }
        }

        private static void AssertListClone(IList source, IList clone, string path)
        {
            Assert.That(clone, Is.Not.SameAs(source), path);
            Assert.That(clone.Count, Is.EqualTo(source.Count), path + ".Count");
            for (int i = 0; i < source.Count; i++)
            {
                AssertCloneGraph(source[i], clone[i], $"{path}[{i}]");
            }
        }

        private static void AssertTypeReferenceClone(TypeReference source, TypeReference clone, string path)
        {
            Assert.That(clone, Is.Not.SameAs(source), path);
            Assert.That(clone.fullName, Is.EqualTo(source.fullName), path + ".fullName");
            Assert.That(clone.assemblyName, Is.EqualTo(source.assemblyName), path + ".assemblyName");
            Assert.That(clone.ReferType, Is.EqualTo(source.ReferType), path + ".ReferType");
            Assert.That(clone.BaseType, Is.EqualTo(source.BaseType), path + ".BaseType");
        }

        private static bool IsAtomic(Type type)
        {
            return type.IsPrimitive ||
                type.IsEnum ||
                type == typeof(string) ||
                type == typeof(decimal) ||
                type == typeof(Type) ||
                type == typeof(UUID) ||
                type == typeof(Vector2) ||
                type == typeof(Vector3) ||
                type == typeof(Vector4) ||
                type == typeof(Color) ||
                type == typeof(Quaternion) ||
                type == typeof(LayerMask);
        }

        private static void AssertMutationIndependent(TreeNode source, TreeNode clone, string path)
        {
            foreach (FieldInfo field in GetCloneFields(source.GetType()))
            {
                AssertMutationIndependentValue(field.GetValue(source), field.GetValue(clone), path + "." + field.Name);
            }
        }

        private static void AssertMutationIndependentValue(object source, object clone, string path)
        {
            if (source == null || clone == null)
            {
                return;
            }

            switch (source)
            {
                case NodeReference sourceReference when clone is NodeReference cloneReference:
                    {
                        UUID sourceUuid = sourceReference.UUID;
                        cloneReference.UUID = UUID.NewUUID();
                        Assert.That(sourceReference.UUID, Is.EqualTo(sourceUuid), path);
                        return;
                    }
                case RawNodeReference sourceReference when clone is RawNodeReference cloneReference:
                    {
                        UUID sourceUuid = sourceReference.UUID;
                        cloneReference.UUID = UUID.NewUUID();
                        Assert.That(sourceReference.UUID, Is.EqualTo(sourceUuid), path);
                        return;
                    }
                case VariableBase sourceVariable when clone is VariableBase cloneVariable:
                    {
                        UUID sourceUuid = sourceVariable.UUID;
                        cloneVariable.SetReference(new VariableData("mutation", cloneVariable.Type));
                        Assert.That(sourceVariable.UUID, Is.EqualTo(sourceUuid), path);
                        return;
                    }
                case GenericTypeReference sourceTypeReference when clone is GenericTypeReference cloneTypeReference:
                    Type sourceBaseType = sourceTypeReference.BaseType;
                    cloneTypeReference.SetBaseType(typeof(UnityEngine.Object));
                    Assert.That(sourceTypeReference.BaseType, Is.EqualTo(sourceBaseType), path);
                    return;
                case TypeReference sourceTypeReference when clone is TypeReference cloneTypeReference:
                    Type sourceReferType = sourceTypeReference.ReferType;
                    cloneTypeReference.SetReferType(typeof(RectTransform));
                    Assert.That(sourceTypeReference.ReferType, Is.EqualTo(sourceReferType), path);
                    return;
                case IList sourceList when clone is IList cloneList:
                    for (int i = 0; i < sourceList.Count; i++)
                    {
                        AssertMutationIndependentValue(sourceList[i], cloneList[i], $"{path}[{i}]");
                    }
                    return;
                case Array sourceArray when clone is Array cloneArray:
                    for (int i = 0; i < sourceArray.Length; i++)
                    {
                        AssertMutationIndependentValue(sourceArray.GetValue(i), cloneArray.GetValue(i), $"{path}[{i}]");
                    }
                    return;
            }

            Type type = source.GetType();
            if (IsAtomic(type) || source is UnityEngine.Object)
            {
                return;
            }

            foreach (FieldInfo field in GetCloneFields(type))
            {
                AssertMutationIndependentValue(field.GetValue(source), field.GetValue(clone), path + "." + field.Name);
            }
        }
    }

    public sealed class DuplicateTests
    {
        [Test]
        public void Duplicate_TypeReference_CreatesEquivalentInstanceWithoutSharedIdentity()
        {
            TypeReference<Component> source = new();
            source.SetReferType(typeof(Transform));

            TypeReference<Component> clone = global::Amlos.AI.Accessors.Duplicate.Value(source);

            Assert.That(clone, Is.Not.SameAs(source));
            Assert.That(clone.fullName, Is.EqualTo(source.fullName));
            Assert.That(clone.assemblyName, Is.EqualTo(source.assemblyName));
            Assert.That(clone.ReferType, Is.EqualTo(source.ReferType));
        }

        [Test]
        public void Duplicate_UnityObject_KeepsSameReference()
        {
            ScriptableObject source = ScriptableObject.CreateInstance<ScriptableObject>();

            try
            {
                ScriptableObject clone = global::Amlos.AI.Accessors.Duplicate.Value(source);

                Assert.That(clone, Is.SameAs(source));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(source);
            }
        }

        [Test]
        public void Duplicate_UnknownMutableReference_Throws()
        {
            Assert.Throws<InvalidOperationException>(() => global::Amlos.AI.Accessors.Duplicate.Value(new UnknownMutableReference()));
        }

        private sealed class UnknownMutableReference
        {
        }
    }

    public sealed class RuntimeSharedDuplicateTests
    {
        [Test]
        public void NodeFactoryDuplicate_RuntimeSharedField_StillDeepClones()
        {
            RuntimeSharedProbeNode source = CreateRuntimeSharedProbeNode();

            RuntimeSharedProbeNode clone = (RuntimeSharedProbeNode)NodeFactory.Duplicate(source);

            Assert.That(clone.sharedPayload, Is.Not.SameAs(source.sharedPayload));
            Assert.That(clone.sharedPayload.value, Is.EqualTo(source.sharedPayload.value));
            Assert.That(clone.localPayload, Is.Not.SameAs(source.localPayload));
            Assert.That(clone.localPayload.value, Is.EqualTo(source.localPayload.value));
        }

        [Test]
        public void NodeFactoryInstantiate_RuntimeSharedField_SharesOnlyMarkedField()
        {
            RuntimeSharedProbeNode source = CreateRuntimeSharedProbeNode();

            RuntimeSharedProbeNode instance = (RuntimeSharedProbeNode)NodeFactory.Instantiate(source);

            Assert.That(instance.sharedPayload, Is.SameAs(source.sharedPayload));
            Assert.That(instance.localPayload, Is.Not.SameAs(source.localPayload));
            Assert.That(instance.localPayload.value, Is.EqualTo(source.localPayload.value));
        }

        private static RuntimeSharedProbeNode CreateRuntimeSharedProbeNode()
        {
            return new RuntimeSharedProbeNode
            {
                name = "runtime shared probe",
                uuid = UUID.NewUUID(),
                parent = new NodeReference(UUID.NewUUID()),
                sharedPayload = new RuntimeSharedPayload { value = 11 },
                localPayload = new RuntimeSharedPayload { value = 23 },
            };
        }
    }

    public sealed class RuntimeSharedProbeNode : TreeNode
    {
        [RuntimeShared]
        public RuntimeSharedPayload sharedPayload = new();

        public RuntimeSharedPayload localPayload = new();

        public override State Execute()
        {
            throw new NotImplementedException();
        }

        public override void Initialize()
        {
            throw new NotImplementedException();
        }

    }

    public sealed class RuntimeSharedPayload : IDuplicable
    {
        public int value;

        public object Duplicate()
        {
            return new RuntimeSharedPayload { value = value };
        }
    }
}
