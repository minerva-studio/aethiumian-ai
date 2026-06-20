#nullable enable
using Amlos.AI.Accessors;
using Amlos.AI.Nodes;
using Amlos.AI.References;
using Minerva.Module;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace Amlos.AI.Tests
{
    public sealed class NodePropertyAccessorRuntimeTests
    {
        [Test]
        public void GetAccessor_WithoutGeneratedRegistryHit_FallsBackToReflectionAccessor()
        {
            DynamicNodeFixture fixture = DynamicNodeFixture.Create(includeRegistry: false);

            NodeAccessor accessor = NodeAccessorProvider.GetAccessor(fixture.NodeType);

            Assert.That(accessor, Is.Not.InstanceOf<NodePropertyAccessor>());
            Assert.That(accessor.NodeType, Is.EqualTo(fixture.NodeType));
        }

        [Test]
        public void GetAccessor_WithGeneratedRegistryHit_ReturnsGeneratedAccessor()
        {
            DynamicNodeFixture fixture = DynamicNodeFixture.Create(includeRegistry: true);

            NodeAccessor accessor = NodeAccessorProvider.GetAccessor(fixture.NodeType);

            Assert.That(accessor, Is.InstanceOf<NodePropertyAccessor>());
            Assert.That(accessor.NodeType, Is.EqualTo(fixture.NodeType));
        }

        [Test]
        public void Clone_WithGeneratedRegistryHit_UsesGeneratedClone()
        {
            DynamicNodeFixture fixture = DynamicNodeFixture.Create(includeRegistry: true);
            TreeNode source = fixture.CreateNode("source");

            TreeNode clone = NodeFactory.Duplicate(source);

            Assert.That(clone.GetType(), Is.EqualTo(fixture.NodeType));
            Assert.That(GetDynamicMarker(clone), Is.EqualTo("generated-clone:source"));
        }

        [Test]
        public void Copy_WithGeneratedRegistryHit_UsesGeneratedCopy()
        {
            DynamicNodeFixture fixture = DynamicNodeFixture.Create(includeRegistry: true);
            TreeNode src = fixture.CreateNode("source");
            TreeNode dst = fixture.CreateNode("destination");

            NodeFactory.Copy(dst, src);

            Assert.That(GetDynamicMarker(dst), Is.EqualTo("generated-copy:source"));
        }

        [Test]
        public void Copy_WithMismatchedRuntimeTypes_ThrowsBeforeCopy()
        {
            DynamicNodeFixture generatedFixture = DynamicNodeFixture.Create(includeRegistry: true);
            DynamicNodeFixture fallbackFixture = DynamicNodeFixture.Create(includeRegistry: false);
            TreeNode src = generatedFixture.CreateNode("source");
            TreeNode dst = fallbackFixture.CreateNode("destination");

            Assert.Throws<ArgumentException>(() => NodeFactory.Copy(dst, src));
            Assert.That(GetDynamicMarker(dst), Is.EqualTo("destination"));
        }

        [Test]
        public void Copy_WithReflectionFallback_DoesNotCopyIdentityOrNodeReferences()
        {
            ReflectionProbeNode src = CreateReflectionProbeNode("source");
            ReflectionProbeNode dst = CreateReflectionProbeNode("destination");
            UUID dstUuid = dst.uuid;
            UUID dstParentUuid = dst.parent.UUID;
            UUID dstChildUuid = dst.child.UUID;

            NodeFactory.Copy(dst, src);

            Assert.That(dst.marker, Is.EqualTo(src.marker));
            Assert.That(dst.name, Is.EqualTo("destination"));
            Assert.That(dst.uuid, Is.EqualTo(dstUuid));
            Assert.That(dst.parent.UUID, Is.EqualTo(dstParentUuid));
            Assert.That(dst.child.UUID, Is.EqualTo(dstChildUuid));
        }

        [Test]
        public void Duplicate_WithReflectionFallback_CopiesIdentityAndNodeReferences()
        {
            ReflectionProbeNode source = CreateReflectionProbeNode("source");

            ReflectionProbeNode clone = (ReflectionProbeNode)NodeFactory.Duplicate(source);

            Assert.That(clone, Is.Not.SameAs(source));
            Assert.That(clone.name, Is.EqualTo(source.name));
            Assert.That(clone.uuid, Is.EqualTo(source.uuid));
            Assert.That(clone.parent.UUID, Is.EqualTo(source.parent.UUID));
            Assert.That(clone.parent, Is.Not.SameAs(source.parent));
            Assert.That(clone.child.UUID, Is.EqualTo(source.child.UUID));
            Assert.That(clone.child, Is.Not.SameAs(source.child));
            Assert.That(clone.children.Count, Is.EqualTo(source.children.Count));
            Assert.That(clone.children[0].UUID, Is.EqualTo(source.children[0].UUID));
            Assert.That(clone.children, Is.Not.SameAs(source.children));
            Assert.That(clone.children[0], Is.Not.SameAs(source.children[0]));
        }

        [Test]
        public void FillNull_WithGeneratedRegistryHit_UsesGeneratedFillNull()
        {
            DynamicNodeFixture fixture = DynamicNodeFixture.Create(includeRegistry: true);
            TreeNode node = fixture.CreateNode(null);

            NodeFactory.FillNull(node);

            Assert.That(GetDynamicMarker(node), Is.EqualTo("generated-fill"));
        }

        public static IReadOnlyList<NodeReferenceAccessor> GetEmptyNodeReferences()
        {
            return Array.Empty<NodeReferenceAccessor>();
        }

        public static IReadOnlyList<NodeReferenceCollectionAccessor> GetEmptyNodeReferenceCollections()
        {
            return Array.Empty<NodeReferenceCollectionAccessor>();
        }

        public static IReadOnlyList<VariableAccessor> GetEmptyVariables()
        {
            return Array.Empty<VariableAccessor>();
        }

        public static IReadOnlyList<VariableCollectionAccessor> GetEmptyVariableCollections()
        {
            return Array.Empty<VariableCollectionAccessor>();
        }

        public static TreeNode CloneDynamicNode(TreeNode source, Type nodeType)
        {
            TreeNode clone = (TreeNode)Activator.CreateInstance(nodeType)!;
            SetDynamicMarker(clone, "generated-clone:" + GetDynamicMarker(source));
            return clone;
        }

        public static void CopyDynamicNode(TreeNode dst, TreeNode src)
        {
            SetDynamicMarker(dst, "generated-copy:" + GetDynamicMarker(src));
        }

        public static void FillNullDynamicNode(TreeNode node)
        {
            if (GetDynamicMarker(node) == null)
            {
                SetDynamicMarker(node, "generated-fill");
            }
        }

        private static ReflectionProbeNode CreateReflectionProbeNode(string marker)
        {
            return new ReflectionProbeNode
            {
                name = marker,
                marker = marker,
                uuid = UUID.NewUUID(),
                parent = new NodeReference(UUID.NewUUID()),
                child = new NodeReference(UUID.NewUUID()),
                children = new List<NodeReference> { new(UUID.NewUUID()) },
                sharedPayload = new ReflectionProbePayload { value = 11 },
                localPayload = new ReflectionProbePayload { value = 23 },
            };
        }

        private static string? GetDynamicMarker(TreeNode node)
        {
            return (string?)node.GetType().GetField("marker")!.GetValue(node);
        }

        private static void SetDynamicMarker(TreeNode node, string? value)
        {
            node.GetType().GetField("marker")!.SetValue(node, value);
        }

        private sealed class DynamicNodeFixture
        {
            private DynamicNodeFixture(Type nodeType)
            {
                NodeType = nodeType;
            }

            public Type NodeType { get; }

            public TreeNode CreateNode(string? marker)
            {
                TreeNode node = (TreeNode)Activator.CreateInstance(NodeType)!;
                SetDynamicMarker(node, marker);
                return node;
            }

            public static DynamicNodeFixture Create(bool includeRegistry)
            {
                // Build a runtime-only assembly so fixture registries never collide with source generator output.
                string suffix = Guid.NewGuid().ToString("N");
                AssemblyName assemblyName = new("Amlos.AI.Tests.GeneratedAccessorFixture." + suffix);
                AssemblyBuilder assembly = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
                ModuleBuilder module = assembly.DefineDynamicModule(assemblyName.Name!);

                Type nodeType = BuildNodeType(module, suffix);
                if (includeRegistry)
                {
                    Type accessorType = BuildAccessorType(module, nodeType, suffix);
                    Type registryType = BuildRegistryType(module, nodeType);

                    NodePropertyAccessor accessor = (NodePropertyAccessor)Activator.CreateInstance(accessorType)!;
                    registryType.GetField("Accessor", BindingFlags.Public | BindingFlags.Static)!.SetValue(null, accessor);
                }

                return new DynamicNodeFixture(nodeType);
            }

            private static Type BuildNodeType(ModuleBuilder module, string suffix)
            {
                TypeBuilder type = module.DefineType(
                    "Amlos.AI.Tests.GeneratedAccessorNode_" + suffix,
                    TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.Class,
                    typeof(TreeNode));

                type.DefineField("marker", typeof(string), FieldAttributes.Public);
                DefineDefaultConstructor(type, typeof(TreeNode));
                DefineInitialize(type);
                DefineExecute(type);
                return type.CreateType();
            }

            private static Type BuildAccessorType(ModuleBuilder module, Type nodeType, string suffix)
            {
                TypeBuilder type = module.DefineType(
                    "Amlos.AI.Accessors.GeneratedAccessorNodePropertyAccessor_" + suffix,
                    TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.Class,
                    typeof(NodePropertyAccessor));

                DefineDefaultConstructor(type, typeof(NodePropertyAccessor));
                DefineNodeTypeGetter(type, nodeType);
                DefineListGetter<NodeReferenceAccessor>(type, nameof(NodeAccessor.NodeReferences), nameof(GetEmptyNodeReferences));
                DefineListGetter<NodeReferenceCollectionAccessor>(type, nameof(NodeAccessor.NodeReferenceCollections), nameof(GetEmptyNodeReferenceCollections));
                DefineListGetter<VariableAccessor>(type, nameof(NodeAccessor.Variables), nameof(GetEmptyVariables));
                DefineListGetter<VariableCollectionAccessor>(type, nameof(NodeAccessor.VariableCollections), nameof(GetEmptyVariableCollections));
                DefineClone(type, nodeType);
                DefineCopy(type);
                DefineFillNull(type);
                return type.CreateType();
            }

            private static Type BuildRegistryType(ModuleBuilder module, Type nodeType)
            {
                TypeBuilder type = module.DefineType(
                    "Amlos.AI.Accessors.GeneratedNodePropertyAccessorRegistry",
                    TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed | TypeAttributes.Class);

                FieldBuilder accessorField = type.DefineField("Accessor", typeof(NodePropertyAccessor), FieldAttributes.Public | FieldAttributes.Static);
                MethodBuilder method = type.DefineMethod(
                    "TryGet",
                    MethodAttributes.Public | MethodAttributes.Static,
                    typeof(bool),
                    new[] { typeof(Type), typeof(NodePropertyAccessor).MakeByRefType() });

                ILGenerator il = method.GetILGenerator();
                Label miss = il.DefineLabel();
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldtoken, nodeType);
                il.Emit(OpCodes.Call, typeof(Type).GetMethod(nameof(Type.GetTypeFromHandle))!);
                il.Emit(OpCodes.Call, typeof(Type).GetMethod("op_Equality", new[] { typeof(Type), typeof(Type) })!);
                il.Emit(OpCodes.Brfalse_S, miss);
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Ldsfld, accessorField);
                il.Emit(OpCodes.Stind_Ref);
                il.Emit(OpCodes.Ldc_I4_1);
                il.Emit(OpCodes.Ret);
                il.MarkLabel(miss);
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Ldnull);
                il.Emit(OpCodes.Stind_Ref);
                il.Emit(OpCodes.Ldc_I4_0);
                il.Emit(OpCodes.Ret);

                return type.CreateType();
            }

            private static void DefineDefaultConstructor(TypeBuilder type, Type baseType)
            {
                ConstructorBuilder ctor = type.DefineConstructor(
                    MethodAttributes.Public,
                    CallingConventions.Standard,
                    Type.EmptyTypes);

                ILGenerator il = ctor.GetILGenerator();
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Call, GetDefaultConstructor(baseType));
                il.Emit(OpCodes.Ret);
            }

            private static ConstructorInfo GetDefaultConstructor(Type type)
            {
                return type.GetConstructor(
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                    null,
                    Type.EmptyTypes,
                    null)!;
            }

            private static void DefineInitialize(TypeBuilder type)
            {
                MethodBuilder method = type.DefineMethod(
                    nameof(TreeNode.Initialize),
                    MethodAttributes.Public | MethodAttributes.Virtual,
                    typeof(void),
                    Type.EmptyTypes);

                method.GetILGenerator().Emit(OpCodes.Ret);
                type.DefineMethodOverride(method, typeof(TreeNode).GetMethod(nameof(TreeNode.Initialize))!);
            }

            private static void DefineExecute(TypeBuilder type)
            {
                MethodBuilder method = type.DefineMethod(
                    nameof(TreeNode.Execute),
                    MethodAttributes.Public | MethodAttributes.Virtual,
                    typeof(State),
                    Type.EmptyTypes);

                ILGenerator il = method.GetILGenerator();
                il.Emit(OpCodes.Ldc_I4_0);
                il.Emit(OpCodes.Ret);
                type.DefineMethodOverride(method, typeof(TreeNode).GetMethod(nameof(TreeNode.Execute))!);
            }

            private static void DefineNodeTypeGetter(TypeBuilder type, Type nodeType)
            {
                MethodBuilder getter = type.DefineMethod(
                    "get_" + nameof(NodeAccessor.NodeType),
                    MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
                    typeof(Type),
                    Type.EmptyTypes);

                ILGenerator il = getter.GetILGenerator();
                il.Emit(OpCodes.Ldtoken, nodeType);
                il.Emit(OpCodes.Call, typeof(Type).GetMethod(nameof(Type.GetTypeFromHandle))!);
                il.Emit(OpCodes.Ret);

                PropertyBuilder property = type.DefineProperty(nameof(NodeAccessor.NodeType), PropertyAttributes.None, typeof(Type), Type.EmptyTypes);
                property.SetGetMethod(getter);
                type.DefineMethodOverride(getter, typeof(NodeAccessor).GetProperty(nameof(NodeAccessor.NodeType))!.GetGetMethod()!);
            }

            private static void DefineListGetter<T>(TypeBuilder type, string propertyName, string helperName)
            {
                Type propertyType = typeof(IReadOnlyList<T>);
                MethodBuilder getter = type.DefineMethod(
                    "get_" + propertyName,
                    MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
                    propertyType,
                    Type.EmptyTypes);

                ILGenerator il = getter.GetILGenerator();
                il.Emit(OpCodes.Call, typeof(NodePropertyAccessorRuntimeTests).GetMethod(helperName, BindingFlags.Public | BindingFlags.Static)!);
                il.Emit(OpCodes.Ret);

                PropertyBuilder property = type.DefineProperty(propertyName, PropertyAttributes.None, propertyType, Type.EmptyTypes);
                property.SetGetMethod(getter);
                type.DefineMethodOverride(getter, typeof(NodeAccessor).GetProperty(propertyName)!.GetGetMethod()!);
            }

            private static void DefineClone(TypeBuilder type, Type nodeType)
            {
                MethodBuilder method = type.DefineMethod(
                    nameof(NodePropertyAccessor.Duplicate),
                    MethodAttributes.Public | MethodAttributes.Virtual,
                    typeof(TreeNode),
                    new[] { typeof(TreeNode), typeof(DuplicateMode) });

                ILGenerator il = method.GetILGenerator();
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Ldtoken, nodeType);
                il.Emit(OpCodes.Call, typeof(Type).GetMethod(nameof(Type.GetTypeFromHandle))!);
                il.Emit(OpCodes.Call, typeof(NodePropertyAccessorRuntimeTests).GetMethod(nameof(CloneDynamicNode), BindingFlags.Public | BindingFlags.Static)!);
                il.Emit(OpCodes.Ret);
                type.DefineMethodOverride(method, typeof(NodePropertyAccessor).GetMethod(nameof(NodePropertyAccessor.Duplicate), new[] { typeof(TreeNode), typeof(DuplicateMode) })!);
            }

            private static void DefineCopy(TypeBuilder type)
            {
                MethodBuilder method = type.DefineMethod(
                    nameof(NodePropertyAccessor.Copy),
                    MethodAttributes.Public | MethodAttributes.Virtual,
                    typeof(void),
                    new[] { typeof(TreeNode), typeof(TreeNode), typeof(DuplicateMode) });

                ILGenerator il = method.GetILGenerator();
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Ldarg_2);
                il.Emit(OpCodes.Call, typeof(NodePropertyAccessorRuntimeTests).GetMethod(nameof(CopyDynamicNode), BindingFlags.Public | BindingFlags.Static)!);
                il.Emit(OpCodes.Ret);
                type.DefineMethodOverride(method, typeof(NodePropertyAccessor).GetMethod(nameof(NodePropertyAccessor.Copy), new[] { typeof(TreeNode), typeof(TreeNode), typeof(DuplicateMode) })!);
            }

            private static void DefineFillNull(TypeBuilder type)
            {
                MethodBuilder method = type.DefineMethod(
                    nameof(NodePropertyAccessor.FillNull),
                    MethodAttributes.Public | MethodAttributes.Virtual,
                    typeof(void),
                    new[] { typeof(TreeNode) });

                ILGenerator il = method.GetILGenerator();
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Call, typeof(NodePropertyAccessorRuntimeTests).GetMethod(nameof(FillNullDynamicNode), BindingFlags.Public | BindingFlags.Static)!);
                il.Emit(OpCodes.Ret);
                type.DefineMethodOverride(method, typeof(NodePropertyAccessor).GetMethod(nameof(NodePropertyAccessor.FillNull), new[] { typeof(TreeNode) })!);
            }
        }

        private sealed class ReflectionProbeNode : TreeNode
        {
            public string marker;
            public NodeReference child;
            public List<NodeReference> children = new();

            [RuntimeShared]
            public ReflectionProbePayload sharedPayload;

            public ReflectionProbePayload localPayload;

            public override State Execute()
            {
                return State.Success;
            }

            public override void Initialize()
            {
            }
        }

        private sealed class ReflectionProbePayload : IDuplicable
        {
            public int value;

            public object Duplicate()
            {
                return new ReflectionProbePayload { value = value };
            }
        }
    }
}
