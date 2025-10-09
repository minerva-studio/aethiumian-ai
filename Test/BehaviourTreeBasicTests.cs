#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using Amlos.AI.Utils;
using NUnit.Framework;
using UnityEngine;

namespace Amlos.AI.Tests
{
    // -------- Unity-like serialization rules (approximation) --------
    static class UnityRules
    {
        public static bool IsUnitySerializableField(FieldInfo f)
        {
            if (f.IsStatic) return false;
            if (Attribute.IsDefined(f, typeof(NonSerializedAttribute))) return false;

            bool isPublicSerialized = f.IsPublic;
            bool hasSerializeField = f.GetCustomAttribute(SerializeFieldType) != null;
            if (!(isPublicSerialized || hasSerializeField)) return false;

            return IsUnitySerializableType(f.FieldType);
        }

        public static bool IsUnitySerializableType(Type t)
        {
            if (t.IsPrimitive || t.IsEnum || t == typeof(string) || t == typeof(decimal))
                return true;

            if (typeof(UnityEngine.Object).IsAssignableFrom(t))
                return true;

            if (t.IsArray) // Unity supports single-rank arrays
                return t.GetArrayRank() == 1 && IsUnitySerializableType(t.GetElementType()!);

            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>))
                return IsUnitySerializableType(t.GetGenericArguments()[0]);

            // allow [Serializable] classes/structs
            return t.IsValueType || t.GetCustomAttribute(typeof(SerializableAttribute)) != null;
        }

        static readonly Type SerializeFieldType =
#if UNITY_5_3_OR_NEWER
            typeof(UnityEngine.SerializeField);
#else
            Type.GetType("UnityEngine.SerializeField, UnityEngine")!;
#endif
    }

    // -------- Reflection helpers --------
    static class Refl
    {
        public static IEnumerable<FieldInfo> UnityFields(Type t) =>
            t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
             .Where(UnityRules.IsUnitySerializableField);

        public static object CreateInstanceLoose(Type t)
        {
            try
            {
                if (t.IsValueType) return Activator.CreateInstance(t)!;
                var ci = t.GetConstructor(Type.EmptyTypes);
                if (ci != null) return ci.Invoke(null);
                return FormatterServices.GetUninitializedObject(t);
            }
            catch
            {
                return FormatterServices.GetUninitializedObject(t);
            }
        }

        public static bool IsAtomic(Type t) =>
            t.IsPrimitive || t.IsEnum || t == typeof(string) ||
            t == typeof(decimal) || t == typeof(DateTime) ||
            t == typeof(DateTimeOffset) || t == typeof(TimeSpan) ||
            t == typeof(Guid);

        public static IEnumerable<Type> SafeTypes(Assembly a)
        {
            try { return a.GetTypes(); }
            catch { return Array.Empty<Type>(); }
        }
    }

    // -------- Simple payloads for nested data (NOT nodes) --------
    [Serializable]
    public class TestAsset : ScriptableObject
    {
        public int id;
    }

    [Serializable]
    public class Payload
    {
        public int number;
        public string label;
        public List<int> ints;
        public TestAsset asset;                // UnityEngine.Object: must keep reference
        [NonSerialized] public int transient;  // must NOT be copied
        [SerializeField] private int _priv;    // must be copied
        public void SetPriv(int v) => _priv = v;
        public int GetPriv() => _priv;
    }

    // Add under your existing [Serializable] payload classes:
    [Serializable]
    public class ParentBox
    {
        public Payload child;           // nested reference (non-Unity object)
        public Payload sameChildRef;    // another field referencing the *same* child object
        public List<Payload> list;      // list with the same child twice (shared ref test)
    }

    [Serializable]
    public class BasePayload
    {
        public int baseValue;
    }

    [Serializable]
    public class DerivedPayload : BasePayload
    {
        [SerializeReference]
        public string extra;  // field only on derived
    }


    [Serializable]
    public class RefNodeLike
    {
        public string tag;
        public List<RefNodeLike> neighbors;
    }

    [Serializable]
    public class SRHolder
    {
        [SerializeReference] public BasePayload refField;
    }


    // -------- Tests --------
    public class TreeNode_Clone_Tests
    {
        Type _treeNodeBase = null!;
        Type _concreteNode = null!;

        [SetUp]
        public void DiscoverTypesAndCloneMethod()
        {
            // Find TreeNode base (non-Unity object)
            _treeNodeBase = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(Refl.SafeTypes)
                .FirstOrDefault(t => t.Name == "TreeNode")
                ?? throw new AssertionException("Could not find type named 'TreeNode'.");

            // Pick a concrete subclass to instantiate (or the base if it's non-abstract)
            _concreteNode = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(Refl.SafeTypes)
                .FirstOrDefault(t => _treeNodeBase.IsAssignableFrom(t) && !t.IsAbstract)
                ?? throw new AssertionException("No non-abstract subclass of TreeNode found to instantiate.");
        }

        [Test]
        public void Clone_DeepCopiesData_KeepsUnityObjectReferences_PreservesSharedRefs()
        {
            // Create a real node instance
            var node = Refl.CreateInstanceLoose(_concreteNode);

            // Shared UnityEngine.Object to test reference-keeping
            var asset = ScriptableObject.CreateInstance<TestAsset>();
            asset.id = 777;

            // Payload for nested references
            var payload = new Payload
            {
                number = 42,
                label = "payload",
                ints = new List<int> { 1, 2, 3 },
                asset = asset,
                transient = 13579
            };
            payload.SetPriv(99);

            // Small graph to test cycles/shared refs
            var a = new RefNodeLike { tag = "A" };
            var b = new RefNodeLike { tag = "B" };
            var c = new RefNodeLike { tag = "C" };
            a.neighbors = new List<RefNodeLike> { b, c };
            b.neighbors = new List<RefNodeLike> { a };      // cycle b->a
            c.neighbors = new List<RefNodeLike> { a, b };   // shares a,b

            // Track assigned fields so we only assert what we actually set
            var setUnityObjects = new List<(FieldInfo f, UnityEngine.Object val)>();
            var setPrimitives = new List<(FieldInfo f, object val)>();
            var setArrays = new List<(FieldInfo f, Array val)>();
            var setLists = new List<(FieldInfo f, IList val)>();
            var setPlainRefs = new List<(FieldInfo f, object val)>();
            var setGraphRoots = new List<(FieldInfo f, RefNodeLike val)>();

            // Assign values to Unity-serializable fields
            foreach (var f in Refl.UnityFields(_concreteNode))
            {
                var ft = f.FieldType;
                try
                {
                    // UnityEngine.Object ¡ú keep reference
                    if (typeof(UnityEngine.Object).IsAssignableFrom(ft))
                    {
                        UnityEngine.Object v = asset;
                        if (ft != typeof(TestAsset) && typeof(ScriptableObject).IsAssignableFrom(ft))
                            v = (UnityEngine.Object)ScriptableObject.CreateInstance(ft);

                        f.SetValue(node, v);
                        setUnityObjects.Add((f, v));
                        continue;
                    }

                    // primitives/enums/strings/etc
                    if (Refl.IsAtomic(ft))
                    {
                        object val = ft == typeof(int) ? 1337 :
                                     ft == typeof(string) ? "hello" :
                                     ft.IsEnum ? Enum.GetValues(ft).GetValue(0)! :
                                     Activator.CreateInstance(ft)!;
                        f.SetValue(node, val);
                        setPrimitives.Add((f, val));
                        continue;
                    }

                    // arrays (single rank)
                    if (ft.IsArray && ft.GetArrayRank() == 1)
                    {
                        var elem = ft.GetElementType()!;
                        var arr = Array.CreateInstance(elem, 2);

                        if (typeof(UnityEngine.Object).IsAssignableFrom(elem))
                        {
                            arr.SetValue(asset, 0);
                            arr.SetValue(asset, 1); // shared
                        }
                        else if (Refl.IsAtomic(elem))
                        {
                            var x = elem == typeof(int) ? 1 : Activator.CreateInstance(elem)!;
                            arr.SetValue(x, 0);
                            arr.SetValue(x, 1);
                        }
                        else if (!elem.IsAbstract && (elem.IsValueType || elem.GetCustomAttribute<SerializableAttribute>() != null))
                        {
                            var p = MakePayloadLike(elem, asset); // shared nested ref
                            arr.SetValue(p, 0);
                            arr.SetValue(p, 1);
                        }

                        f.SetValue(node, arr);
                        setArrays.Add((f, arr));
                        continue;
                    }

                    // List<T>
                    if (ft.IsGenericType && ft.GetGenericTypeDefinition() == typeof(List<>))
                    {
                        var elem = ft.GetGenericArguments()[0];
                        var listObj = (IList)Activator.CreateInstance(ft)!;

                        if (typeof(UnityEngine.Object).IsAssignableFrom(elem))
                        {
                            listObj.Add(asset);
                            listObj.Add(asset); // shared
                        }
                        else if (Refl.IsAtomic(elem))
                        {
                            listObj.Add(elem == typeof(int) ? 1 : Activator.CreateInstance(elem)!);
                            listObj.Add(elem == typeof(int) ? 1 : Activator.CreateInstance(elem)!);
                        }
                        else if (!elem.IsAbstract && (elem.IsValueType || elem.GetCustomAttribute<SerializableAttribute>() != null))
                        {
                            var p = MakePayloadLike(elem, asset);
                            listObj.Add(p);
                            listObj.Add(p); // shared
                        }

                        f.SetValue(node, listObj);
                        setLists.Add((f, listObj));
                        continue;
                    }

                    // Try graph root for cycles/shared-refs if compatible
                    if (!ft.IsValueType && ft.GetCustomAttribute<SerializableAttribute>() != null &&
                        ft.IsAssignableFrom(typeof(RefNodeLike)))
                    {
                        f.SetValue(node, a);
                        setGraphRoots.Add((f, a));
                        continue;
                    }

                    // Generic serializable reference
                    if (!ft.IsValueType && ft.GetCustomAttribute<SerializableAttribute>() != null)
                    {
                        object val;
                        if (ft.IsAssignableFrom(typeof(Payload)))
                        {
                            val = payload;
                        }
                        else
                        {
                            val = Refl.CreateInstanceLoose(ft);
                            TryPopulatePayloadLike(val, asset);
                        }
                        f.SetValue(node, val);
                        setPlainRefs.Add((f, val));
                        continue;
                    }
                }
                catch
                {
                    // Ignore fields we cannot safely assign.
                }
            }

            Assert.That(
                setUnityObjects.Count + setPrimitives.Count + setArrays.Count + setLists.Count + setPlainRefs.Count + setGraphRoots.Count,
                Is.GreaterThan(0),
                "No serializable fields could be assigned on the chosen TreeNode type; cannot validate clone behavior."
            );

            // Invoke NodeFactory.Clone(node, node.GetType())
            var cloned = DeepClone.Clone(node) ?? throw new AssertionException("NodeFactory.Clone returned null.");
            // _cloneMI.Invoke(null, new object[] { node, node.GetType() })

            // UnityEngine.Object fields: same reference
            foreach (var (f, val) in setUnityObjects)
            {
                var cv = (UnityEngine.Object?)f.GetValue(cloned);
                Assert.That(ReferenceEquals(cv, val), Is.True, $"UnityEngine.Object field '{f.Name}' should keep reference.");
            }

            // Primitives/strings/enums: equal by value
            foreach (var (f, val) in setPrimitives)
            {
                var cv = f.GetValue(cloned);
                Assert.That(Equals(cv, val), Is.True, $"Field '{f.Name}' should copy value.");
            }

            // Arrays: new instance; preserve intra-array shared references
            foreach (var (f, arr) in setArrays)
            {
                var carr = (Array)f.GetValue(cloned)!;
                Assert.That(ReferenceEquals(carr, arr), Is.False, $"Array field '{f.Name}' should be a new instance.");
                Assert.That(carr.Length, Is.EqualTo(arr.Length));
                if (arr.Length >= 2)
                {
                    var a0 = arr.GetValue(0);
                    var a1 = arr.GetValue(1);
                    var c0 = carr.GetValue(0);
                    var c1 = carr.GetValue(1);
                    if (a0 != null && ReferenceEquals(a0, a1))
                        Assert.That(ReferenceEquals(c0, c1), Is.True, $"Array field '{f.Name}' should preserve shared element references.");
                }
            }

            // Lists: new instance; preserve intra-list shared references
            foreach (var (f, listObj) in setLists)
            {
                var cl = (IList)f.GetValue(cloned)!;
                Assert.That(ReferenceEquals(cl, listObj), Is.False, $"List field '{f.Name}' should be a new instance.");
                Assert.That(cl.Count, Is.EqualTo(((IList)listObj).Count));
                if (cl.Count >= 2)
                {
                    var a0 = ((IList)listObj)[0];
                    var a1 = ((IList)listObj)[1];
                    var c0 = cl[0];
                    var c1 = cl[1];
                    if (a0 != null && ReferenceEquals(a0, a1))
                        Assert.That(ReferenceEquals(c0, c1), Is.True, $"List field '{f.Name}' should preserve shared element references.");
                }
            }

            // Plain serializable references: new instances; nested UnityEngine.Object kept by reference
            foreach (var (f, obj) in setPlainRefs)
            {
                var cv = f.GetValue(cloned);
                Assert.That(ReferenceEquals(cv, obj), Is.False, $"Field '{f.Name}' should be a new reference.");

                if (obj is Payload p && cv is Payload pc)
                {
                    Assert.That(pc.number, Is.EqualTo(42));
                    Assert.That(pc.label, Is.EqualTo("payload"));
                    CollectionAssert.AreEqual(new[] { 1, 2, 3 }, pc.ints);
                    Assert.That(pc.GetPriv(), Is.EqualTo(99));
                    Assert.That(pc.transient, Is.EqualTo(0)); // NonSerialized not copied
                    Assert.That(ReferenceEquals(pc.asset, p.asset), Is.True, "Nested UnityEngine.Object should keep reference.");
                }
            }

            // Graph roots: cycles and shared references preserved
            foreach (var (f, root) in setGraphRoots)
            {
                var clonedRoot = (RefNodeLike)f.GetValue(cloned)!;
                Assert.That(clonedRoot.tag, Is.EqualTo("A"));
                Assert.That(clonedRoot.neighbors[0].tag, Is.EqualTo("B"));
                Assert.That(clonedRoot.neighbors[1].tag, Is.EqualTo("C"));
                var b2 = clonedRoot.neighbors[0];
                Assert.That(ReferenceEquals(b2.neighbors[0], clonedRoot), Is.True, $"Cycle should be preserved for field '{f.Name}'.");
            }
        }
        // Add this test method inside class TreeNode_Clone_Tests
        [Test]
        public void DeepClone_NestedObjectReference_InnerObjectIsDeepCloned_AndSharedRefsPreserved()
        {
            // Build nested structure: ParentBox -> Payload (inner), with shared refs
            var asset = ScriptableObject.CreateInstance<TestAsset>();
            asset.id = 888;

            var child = new Payload
            {
                number = 11,
                label = "child",
                ints = new List<int> { 9, 8, 7 },
                asset = asset
            };
            child.SetPriv(55);

            var parent = new ParentBox
            {
                child = child,
                sameChildRef = child,                       // shared reference to the same inner object
                list = new List<Payload> { child, child } // shared reference inside a list
            };

            // Clone the parent directly (no TreeNode involved)
            var parentClone = DeepClone.Clone(parent);
            Assert.That(parentClone, Is.Not.Null);

            // Outer object must be a new instance
            Assert.That(ReferenceEquals(parentClone, parent), Is.False);

            // Inner object must be a new instance (deep clone)
            Assert.That(ReferenceEquals(parentClone.child, child), Is.False);
            Assert.That(parentClone.child.number, Is.EqualTo(11));
            Assert.That(parentClone.child.label, Is.EqualTo("child"));
            CollectionAssert.AreEqual(new[] { 9, 8, 7 }, parentClone.child.ints);

            // UnityEngine.Object inside the inner object must keep reference
            Assert.That(ReferenceEquals(parentClone.child.asset, asset), Is.True);

            // Shared references preserved inside the object
            Assert.That(ReferenceEquals(parentClone.child, parentClone.sameChildRef), Is.True);

            // List is deep-copied (new list), but elements share the same cloned child instance
            Assert.That(ReferenceEquals(parentClone.list, parent.list), Is.False);
            Assert.That(parentClone.list.Count, Is.EqualTo(2));
            Assert.That(ReferenceEquals(parentClone.list[0], parentClone.child), Is.True);
            Assert.That(ReferenceEquals(parentClone.list[1], parentClone.child), Is.True);

            // Mutate source to ensure clone independence
            child.ints.Add(999);
            CollectionAssert.AreEqual(new[] { 9, 8, 7 }, parentClone.child.ints);
        }

        [Test]
        public void DeepClone_SerializeReferenceField_WithDerivedInstance_PreservesRuntimeTypeAndFields()
        {
            // Prepare derived instance
            var derived = new DerivedPayload
            {
                baseValue = 5,
                extra = "hello"
            };

            // Put it under a SerializeReference field so polymorphism is respected
            var holder = new SRHolder { refField = derived };

            // Clone the holder directly
            var holderClone = DeepClone.Clone(holder);
            Assert.That(holderClone, Is.Not.Null);

            // The managed reference should retain its runtime type and data
            Assert.That(holderClone.refField, Is.Not.Null);
            Assert.That(holderClone.refField.GetType(), Is.EqualTo(typeof(DerivedPayload)));

            var d2 = (DerivedPayload)holderClone.refField;
            Assert.That(d2.baseValue, Is.EqualTo(5));
            Assert.That(d2.extra, Is.EqualTo("hello"));

            // Ensure new instance (not aliasing the original)
            Assert.That(ReferenceEquals(holderClone.refField, holder.refField), Is.False);
        }


        // -------- Utilities to synthesize payload-like instances --------
        static object MakePayloadLike(Type target, TestAsset sharedAsset)
        {
            var obj = Refl.CreateInstanceLoose(target);
            TryPopulatePayloadLike(obj, sharedAsset);
            return obj;
        }

        static void TryPopulatePayloadLike(object instance, TestAsset sharedAsset)
        {
            var t = instance.GetType();
            foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (Attribute.IsDefined(f, typeof(NonSerializedAttribute))) continue;
                try
                {
                    if (f.FieldType == typeof(int)) f.SetValue(instance, 42);
                    else if (f.FieldType == typeof(string)) f.SetValue(instance, "payload");
                    else if (f.FieldType == typeof(List<int>)) f.SetValue(instance, new List<int> { 1, 2, 3 });
                    else if (typeof(UnityEngine.Object).IsAssignableFrom(f.FieldType)) f.SetValue(instance, sharedAsset);
                }
                catch { /* ignore */ }
            }
            // Try a common private int named like "_priv"
            var priv = t.GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                        .FirstOrDefault(ff => ff.FieldType == typeof(int) && ff.Name.IndexOf("priv", StringComparison.OrdinalIgnoreCase) >= 0);
            if (priv != null) { try { priv.SetValue(instance, 99); } catch { } }
        }
    }
}
