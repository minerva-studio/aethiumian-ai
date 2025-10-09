#nullable enable 
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using Unity.Collections.LowLevel.Unsafe;

namespace Amlos.AI.Utils
{
    public static class DeepClone
    {
        // Cache a compiled cloner per runtime type: (obj, visited) => clone
        static readonly ConcurrentDictionary<Type, Delegate> _compiled = new();

        // Caches for traits
        static readonly ConcurrentDictionary<Type, bool> _atomicLikeCache = new();
        static readonly ConcurrentDictionary<Type, bool> _noRefsValueTypeCache = new();

        // Cached MethodInfos / delegates
        static readonly MethodInfo _miDeepCloneInternal = typeof(DeepClone)
            .GetMethod(nameof(DeepCloneInternal), BindingFlags.NonPublic | BindingFlags.Static)!;

        static readonly MethodInfo _miIsNoRefsGeneric = typeof(DeepClone)
            .GetMethod(nameof(IsNoRefsGeneric), BindingFlags.NonPublic | BindingFlags.Static)!;

        // Very small thread-local pool for visited dictionaries to reduce allocations
        [ThreadStatic] static Stack<Dictionary<object, object>>? _visitedPool;

        static Dictionary<object, object> RentVisited()
        {
            var pool = _visitedPool ??= new Stack<Dictionary<object, object>>(4);
            if (pool.Count > 0) return pool.Pop();
            return new Dictionary<object, object>(ReferenceEqualityComparer.Instance);
        }

        static void ReturnVisited(Dictionary<object, object>? dict)
        {
            if (dict == null) return;
            dict.Clear();
            (_visitedPool ??= new Stack<Dictionary<object, object>>(4)).Push(dict);
        }

        /// <summary>
        /// Deep clone that follows Unity's serialization rules.
        /// UnityEngine.Object references are kept (no copy/Instantiate).
        /// </summary>
        public static T Clone<T>(T source)
        {
            // Fast path: if T is "atomic-like", no allocations at all.
            if (TypeTraits<T>.IsAtomicLike) return source;

            // Lazy-allocate visited only when we actually clone a reference graph.
            Dictionary<object, object>? visited = null;
            try
            {
                return DeepCloneInternal(source, ref visited);
            }
            finally
            {
                ReturnVisited(visited);
            }
        }

        // NOTE: visited is lazily created inside DeepCloneRef only when needed.
        static T DeepCloneInternal<T>(T source, ref Dictionary<object, object>? visited)
        {
            if (source is null) return default!;

            // If T is "atomic-like", no recursion, no visited.
            if (TypeTraits<T>.IsAtomicLike) return source;

            // Value types that are not atomic-like still need deep-clone if they contain refs.
            if (!TypeTraits<T>.IsReferenceType)
            {
                var boxed = (object)source!;
                return (T)DeepCloneRef(boxed, ref visited)!;
            }

            // Reference type
            return (T)DeepCloneRef(source!, ref visited)!;
        }

        static object? DeepCloneRef(object obj, ref Dictionary<object, object>? visited)
        {
            if (obj is null) return null;

            var type = obj.GetType();

#if UNITY_5_3_OR_NEWER
            // Keep UnityEngine.Object references as-is
            if (obj is UnityEngine.Object) return obj;
#endif
            // Atomic-like runtime types
            if (IsAtomicLike(type)) return obj;

            // Lazy create visited only if we actually clone a non-atomic reference
            visited ??= RentVisited();

            // Handle shared references / cycles
            if (visited.TryGetValue(obj, out var existing)) return existing;

            // JIT-compile or reuse a per-type boxed cloner (based on runtime type)
            var cloner = (Func<object, Dictionary<object, object>, object>)_compiled.GetOrAdd(
                type,
                static t => BuildBoxedCloner(t)
            );

            var clone = cloner(obj, visited);
            return clone;
        }

        // ---------- Type traits ----------

        static bool IsAtomicLike(Type t)
        {
            if (_atomicLikeCache.TryGetValue(t, out var cached)) return cached;

            bool result =
                t.IsPrimitive || t.IsEnum ||
                t == typeof(string) ||
                t == typeof(decimal) ||
                t == typeof(DateTime) ||
                t == typeof(DateTimeOffset) ||
                t == typeof(TimeSpan) ||
                t == typeof(Guid) ||
                IsNoRefsValueType(t); // value types with no managed refs are atomic-like

            _atomicLikeCache[t] = result;
            return result;
        }

        // Returns true for value types that are “no-refs” (thus safe to treat atomic-like).
        static bool IsNoRefsValueType(Type t)
        {
            if (!t.IsValueType) return false;
            if (_noRefsValueTypeCache.TryGetValue(t, out var cached)) return cached;

            bool result;
            try
            {
                // Preferred: RuntimeHelpers.IsReferenceOrContainsReferences<T>() via cached generic delegate
                var del = _noRefsGenericDelegates.GetOrAdd(t, static tt =>
                {
                    var g = _miIsNoRefsGeneric.MakeGenericMethod(tt);
                    return (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), g);
                });
                result = del();
            }
            catch
            {
                // Fallback: Unity's UnsafeUtility.IsBlittable (interop-oriented but good hint)
                try { result = UnsafeUtility.IsBlittable(t); }
                catch
                {
                    // Last-resort heuristic: value type with only value-type fields
                    result = !t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                              .Any(f => !f.FieldType.IsValueType);
                }
            }

            _noRefsValueTypeCache[t] = result;
            return result;
        }

        static readonly ConcurrentDictionary<Type, Func<bool>> _noRefsGenericDelegates = new();

        static bool IsNoRefsGeneric<T>() => !RuntimeHelpers.IsReferenceOrContainsReferences<T>();

        // ---------- Cloner builders ----------

        // Build (object obj, Dictionary<object,object> visited) => object clone for concrete type
        static Delegate BuildBoxedCloner(Type t)
        {
            // Specialized cloners for common containers first
            if (t.IsArray) return BuildArrayBoxedCloner(t);
            if (IsGenericList(t)) return BuildListBoxedCloner(t);

            // General reference type (including boxed structs/classes marked [Serializable])
            var objParam = Expression.Parameter(typeof(object), "obj");
            var visitedParam = Expression.Parameter(typeof(Dictionary<object, object>), "visited");

            var typedObj = Expression.Variable(t, "typed");
            var cloneVar = Expression.Variable(t, "clone");

            var assignTyped = Expression.Assign(typedObj, Expression.Convert(objParam, t));

            var block = new List<Expression> { assignTyped };

            // Allocate new instance without invoking constructors to mimic Unity's deserialization
            var newInst = CreateUninitialized(t);
            block.Add(Expression.Assign(cloneVar, newInst));

            // visited[obj] = clone
            var visitedAdd = Expression.Call(visitedParam,
                typeof(Dictionary<object, object>).GetMethod("Add")!,
                Expression.Convert(typedObj, typeof(object)),
                Expression.Convert(cloneVar, typeof(object)));
            block.Add(visitedAdd);

            // Assign all fields that Unity would serialize: clone.f = DeepClone(fieldValue)
            foreach (var f in UnitySerialization.GetUnitySerializedFields(t))
            {
                var srcField = Expression.Field(typedObj, f);
                var dstField = Expression.Field(cloneVar, f);

                Expression valueExpr = BuildFieldCloneExpression(f.FieldType, srcField, visitedParam);
                block.Add(Expression.Assign(dstField, valueExpr));
            }

            // Return boxed clone
            block.Add(Expression.Convert(cloneVar, typeof(object)));

            var body = Expression.Block(new[] { typedObj, cloneVar }, block);
            var lambda = Expression.Lambda<Func<object, Dictionary<object, object>, object>>(body, objParam, visitedParam);
            return lambda.Compile();
        }

        static Expression CreateUninitialized(Type t)
        {
            // Classes: FormatterServices.GetUninitializedObject to avoid ctor side effects
            // Structs: default(T)
            if (!t.IsValueType)
            {
                var mi = typeof(System.Runtime.Serialization.FormatterServices)
                    .GetMethod(nameof(System.Runtime.Serialization.FormatterServices.GetUninitializedObject))!;
                return Expression.Convert(Expression.Call(mi, Expression.Constant(t, typeof(Type))), t);
            }
            else
            {
                return Expression.Default(t);
            }
        }

        static bool IsGenericList(Type t)
            => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>);

        static Delegate BuildArrayBoxedCloner(Type arrType)
        {
            // Unity serializes only single-dimension, zero-based arrays
            var elem = arrType.GetElementType()!;
            var objParam = Expression.Parameter(typeof(object), "obj");
            var visitedParam = Expression.Parameter(typeof(Dictionary<object, object>), "visited");

            var typed = Expression.Variable(arrType, "src");
            var clone = Expression.Variable(arrType, "dst");
            var len = Expression.Variable(typeof(int), "len");
            var i = Expression.Variable(typeof(int), "i");

            var assignTyped = Expression.Assign(typed, Expression.Convert(objParam, arrType));
            var length = Expression.Property(typed, "Length");
            var assignLen = Expression.Assign(len, length);
            var newArr = Expression.NewArrayBounds(elem, len);
            var assignClone = Expression.Assign(clone, newArr);

            // visited[src] = clone
            var visitedAdd = Expression.Call(visitedParam,
                typeof(Dictionary<object, object>).GetMethod("Add")!,
                Expression.Convert(typed, typeof(object)),
                Expression.Convert(clone, typeof(object)));

            // If element is atomic-like (incl. no-refs struct), we can bulk copy
            if (IsAtomicLike(elem))
            {
                // Array.Copy(src, dst, len)
                var arrayCopy = Expression.Call(
                    typeof(Array).GetMethod("Copy", new[] { typeof(Array), typeof(Array), typeof(int) })!,
                    Expression.Convert(typed, typeof(Array)),
                    Expression.Convert(clone, typeof(Array)),
                    len
                );

                var bodyFast = Expression.Block(
                    new[] { typed, clone, len },
                    assignTyped, assignLen, assignClone, visitedAdd,
                    arrayCopy,
                    Expression.Convert(clone, typeof(object))
                );

                return Expression.Lambda<Func<object, Dictionary<object, object>, object>>(bodyFast, objParam, visitedParam).Compile();
            }

            // Otherwise per-element deep clone
            var breakLbl = Expression.Label("brk");
            var loopBody = new List<Expression>
            {
                Expression.IfThen(Expression.GreaterThanOrEqual(i, len), Expression.Break(breakLbl))
            };
            var srcAtI = Expression.ArrayIndex(typed, i);
            var clonedElem = BuildFieldCloneExpression(elem, srcAtI, visitedParam);
            var setDst = Expression.Assign(Expression.ArrayAccess(clone, i), clonedElem);
            loopBody.Add(setDst);
            loopBody.Add(Expression.PostIncrementAssign(i));
            var loop = Expression.Loop(Expression.Block(loopBody), breakLbl);

            var body = Expression.Block(
                new[] { typed, clone, len, i },
                assignTyped, assignLen, assignClone, visitedAdd,
                Expression.Assign(i, Expression.Constant(0)),
                loop,
                Expression.Convert(clone, typeof(object))
            );

            return Expression.Lambda<Func<object, Dictionary<object, object>, object>>(body, objParam, visitedParam).Compile();
        }

        static Delegate BuildListBoxedCloner(Type listType)
        {
            var elem = listType.GetGenericArguments()[0];
            var objParam = Expression.Parameter(typeof(object), "obj");
            var visitedParam = Expression.Parameter(typeof(Dictionary<object, object>), "visited");

            var typed = Expression.Variable(listType, "src");
            var clone = Expression.Variable(listType, "dst");

            var assignTyped = Expression.Assign(typed, Expression.Convert(objParam, listType));
            var ctor = listType.GetConstructor(Type.EmptyTypes)
                      ?? throw new InvalidOperationException($"{listType} needs a public parameterless ctor.");

            var newList = Expression.New(ctor);
            var assignClone = Expression.Assign(clone, newList);

            // visited[src] = clone
            var visitedAdd = Expression.Call(visitedParam,
                typeof(Dictionary<object, object>).GetMethod("Add")!,
                Expression.Convert(typed, typeof(object)),
                Expression.Convert(clone, typeof(object)));

            var addMI = listType.GetMethod("Add")!;
            var countGet = listType.GetProperty("Count")!.GetGetMethod()!;
            var capSet = listType.GetProperty("Capacity")?.GetSetMethod();

            // If element is atomic-like, we can skip recursive clone per item
            if (IsAtomicLike(elem))
            {
                // dst.Capacity = src.Count (if available)
                var setCapExpr = capSet != null
                    ? Expression.Call(clone, capSet, Expression.Call(typed, countGet))
                    : (Expression)Expression.Empty();

                var xVar = Expression.Variable(elem, "x");
                var loop = ForEach(
                    typed,
                    xVar,
                    Expression.Call(clone, addMI, xVar) // add as-is
                );

                var bodyFast = Expression.Block(
                    new[] { typed, clone, xVar },
                    assignTyped, assignClone, visitedAdd,
                    setCapExpr,
                    loop,
                    Expression.Convert(clone, typeof(object))
                );

                return Expression.Lambda<Func<object, Dictionary<object, object>, object>>(bodyFast, objParam, visitedParam).Compile();
            }

            // Else, deep-clone each element
            var xVarDeep = Expression.Variable(elem, "x");
            var loopDeep = ForEach(
                typed,
                xVarDeep,
                Expression.Call(clone, addMI, BuildFieldCloneExpression(elem, xVarDeep, visitedParam))
            );

            var body = Expression.Block(
                new[] { typed, clone, xVarDeep },
                assignTyped, assignClone, visitedAdd, loopDeep,
                Expression.Convert(clone, typeof(object))
            );

            return Expression.Lambda<Func<object, Dictionary<object, object>, object>>(body, objParam, visitedParam).Compile();
        }

        // Build expression that clones a field value (valueExpr) to a cloned value according to rules
        static Expression BuildFieldCloneExpression(Type fieldType, Expression valueExpr, ParameterExpression visitedParam)
        {
#if UNITY_5_3_OR_NEWER
            // Unity objects: keep original reference
            if (typeof(UnityEngine.Object).IsAssignableFrom(fieldType))
            {
                return valueExpr;
            }
#endif
            // Atomic types: return as-is
            if (IsAtomicLike(fieldType))
            {
                return valueExpr;
            }

            // Complex/reference types: call DeepCloneInternal<FT>(value, visited)
            var gMethod = _miDeepCloneInternal.MakeGenericMethod(fieldType);
            // NOTE: Even if FT is a base/interface, DeepCloneInternal routes to DeepCloneRef by runtime type.
            return Expression.Call(gMethod, valueExpr, visitedParam);
        }

        // Generic foreach for IEnumerable<T>
        static Expression ForEach(Expression enumerable, ParameterExpression loopVar, Expression loopContent)
        {
            var getEnumerator = typeof(IEnumerable<>).MakeGenericType(loopVar.Type).GetMethod("GetEnumerator")!;
            var enumeratorType = typeof(IEnumerator<>).MakeGenericType(loopVar.Type);
            var moveNext = typeof(IEnumerator).GetMethod("MoveNext")!;
            var current = enumeratorType.GetProperty("Current")!.GetGetMethod()!;

            var enumerator = Expression.Variable(enumeratorType, "en");
            var assignEnum = Expression.Assign(enumerator, Expression.Call(enumerable, getEnumerator));

            var breakLbl = Expression.Label("LoopBreak");

            var loop = Expression.Loop(
                Expression.IfThenElse(
                    Expression.Call(enumerator, moveNext),
                    Expression.Block(
                        new[] { loopVar },
                        Expression.Assign(loopVar, Expression.Call(enumerator, current)),
                        loopContent
                    ),
                    Expression.Break(breakLbl)
                ),
                breakLbl
            );

            // Ensure enumerator is disposed
            var dispose = typeof(IDisposable).GetMethod("Dispose");
            var finallyBlock = Expression.IfThen(
                Expression.TypeIs(enumerator, typeof(IDisposable)),
                Expression.Call(Expression.Convert(enumerator, typeof(IDisposable)), dispose!)
            );

            return Expression.Block(new[] { enumerator }, assignEnum, Expression.TryFinally(loop, finallyBlock));
        }

        // Reference equality comparer for the visited dictionary
        sealed class ReferenceEqualityComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceEqualityComparer Instance = new();
            public new bool Equals(object x, object y) => ReferenceEquals(x, y);
            public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
        }

        // Per-T traits resolved once and inlined in generic paths
        static class TypeTraits<T>
        {
            public static readonly bool IsReferenceType = !typeof(T).IsValueType;

            // Use runtime helpers directly for T to avoid any Type-based reflection for the hot path.
            public static readonly bool IsNoRefsValueType =
                typeof(T).IsValueType && !RuntimeHelpers.IsReferenceOrContainsReferences<T>();

            public static readonly bool IsAtomicLike =
                typeof(T).IsPrimitive || typeof(T).IsEnum ||
                typeof(T) == typeof(string) ||
                typeof(T) == typeof(decimal) ||
                typeof(T) == typeof(DateTime) ||
                typeof(T) == typeof(DateTimeOffset) ||
                typeof(T) == typeof(TimeSpan) ||
                typeof(T) == typeof(Guid) ||
                IsNoRefsValueType;
        }
    }
}
