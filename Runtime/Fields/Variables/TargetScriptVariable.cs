using System;
using System.Linq.Expressions;
using System.Reflection;
using UnityEngine;
using static Aethiumian.AI.Variables.VariableUtility;

namespace Aethiumian.AI.Variables
{
    [Serializable]
    public class TargetScriptVariable : Variable
    {
        [Header("Field Reference to target script")]
        private MemberInfo member;
        private object targetInstance;
        private ITargetScriptAccessor accessor;

        public Type objectType;
        public VariableType type;


        /// <summary>
        /// Creates a variable backed by a member on the target instance.
        /// </summary>
        public TargetScriptVariable(VariableData data, object target) : base(data.UUID, data.name)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            member = target.GetType().GetMember(data.Path)[0];
            targetInstance = target;
            Init();
        }


        /// <summary>
        /// Reads the member through the explicit boxed compatibility boundary.
        /// </summary>
        public override object Value => accessor.ReadBoxed();
        public override VariableType Type => type;
        public override Type ObjectType => objectType;
        public object Target => targetInstance;
        public MemberInfo Member => member;

        public override T GetValue<T>() => accessor.Read<T>();

        public override void SetValue<T>(T value) => accessor.Write(value);

        /// <summary>
        /// Resolves the real member type and creates the cached typed accessor.
        /// </summary>
        public void Init()
        {
            objectType = GetAccessorType(member);
            type = GetVariableType(objectType);
            accessor = TargetScriptAccessorFactory.Create(member, targetInstance, objectType);
        }

        /// <summary>
        /// Gets the value type for a readable member or the parameter type for a setter method.
        /// </summary>
        private static Type GetAccessorType(MemberInfo member)
        {
            if (member is MethodInfo method && method.GetParameters().Length == 1)
            {
                return method.GetParameters()[0].ParameterType;
            }

            return GetResultType(member);
        }
    }

    /// <summary>
    /// Provides a type-erased owner for a strongly typed target-script member accessor.
    /// </summary>
    internal interface ITargetScriptAccessor
    {
        /// <summary>
        /// Reads and converts the member without crossing the object boundary.
        /// </summary>
        TTarget Read<TTarget>();

        /// <summary>
        /// Converts and writes a value to the member without using object storage.
        /// </summary>
        void Write<TSource>(TSource value);

        /// <summary>
        /// Reads the member as an object for the compatibility API.
        /// </summary>
        object ReadBoxed();
    }

    /// <summary>
    /// Stores delegates closed over the real reflected member type.
    /// </summary>
    internal sealed class TargetScriptAccessor<TSource> : ITargetScriptAccessor
    {
        private readonly Func<TSource> getter;
        private readonly Action<TSource> setter;

        /// <summary>
        /// Creates a typed accessor. A missing delegate represents an unsupported direction.
        /// </summary>
        internal TargetScriptAccessor(Func<TSource> getter, Action<TSource> setter)
        {
            this.getter = getter;
            this.setter = setter;
        }

        /// <inheritdoc />
        public TTarget Read<TTarget>()
        {
            if (getter == null)
            {
                throw new InvalidOperationException("The target script member is not readable.");
            }

            return ImplicitConverter<TTarget>.From(getter());
        }

        /// <inheritdoc />
        public void Write<TValue>(TValue value)
        {
            if (setter == null)
            {
                throw new InvalidOperationException("The target script member is not writable.");
            }

            setter(ImplicitConverter<TSource>.From(value));
        }

        /// <inheritdoc />
        public object ReadBoxed()
        {
            if (getter == null)
            {
                throw new InvalidOperationException("The target script member is not readable.");
            }

            return getter();
        }
    }

    /// <summary>
    /// Builds one cached accessor for a target-script member during initialization.
    /// </summary>
    internal static class TargetScriptAccessorFactory
    {
        private static readonly object[] EmptyArguments = Array.Empty<object>();

        /// <summary>
        /// Creates an accessor closed over the member's real CLR type.
        /// </summary>
        internal static ITargetScriptAccessor Create(MemberInfo member, object target, Type sourceType)
        {
            if (sourceType == null || sourceType == typeof(void))
            {
                throw new InvalidOperationException($"Member '{member?.Name}' has no readable value type.");
            }

            MethodInfo factory = typeof(TargetScriptAccessorFactory).GetMethod(
                nameof(CreateTyped), BindingFlags.NonPublic | BindingFlags.Static);
            return (ITargetScriptAccessor)factory
                .MakeGenericMethod(sourceType)
                .Invoke(null, new object[] { member, target });
        }

        /// <summary>
        /// Creates the closed generic accessor after the real member type is known.
        /// </summary>
        private static ITargetScriptAccessor CreateTyped<TSource>(MemberInfo member, object target)
        {
            Func<TSource> getter = CreateGetter<TSource>(member, target);
            Action<TSource> setter = CreateSetter<TSource>(member, target);
            return new TargetScriptAccessor<TSource>(getter, setter);
        }

        /// <summary>
        /// Selects a direct getter or an initialization-time reflection fallback.
        /// </summary>
        private static Func<TSource> CreateGetter<TSource>(MemberInfo member, object target)
        {
            switch (member)
            {
                case PropertyInfo property:
                    {
                        MethodInfo getter = property.GetGetMethod(true);
                        if (getter == null || property.GetIndexParameters().Length != 0)
                        {
                            return null;
                        }

                        if (getter.ReturnType == typeof(TSource))
                        {
                            try
                            {
                                return BindGetter<TSource>(getter, target);
                            }
                            catch (Exception)
                            {
                                // Fall through to the reflection fallback selected at initialization.
                            }
                        }

                        return CreateReflectionGetter<TSource>(property, target);
                    }
                case MethodInfo method:
                    if (method.GetParameters().Length == 0 && method.ReturnType == typeof(TSource))
                    {
                        try
                        {
                            return BindGetter<TSource>(method, target);
                        }
                        catch (Exception)
                        {
                            // Fall through to the reflection fallback selected at initialization.
                        }
                    }

                    return method.GetParameters().Length == 0
                        ? CreateReflectionGetter<TSource>(method, target)
                        : null;
                case FieldInfo field:
                    return CreateFieldGetter<TSource>(field, target);
                default:
                    return null;
            }
        }

        /// <summary>
        /// Selects a direct setter or an initialization-time reflection fallback.
        /// </summary>
        private static Action<TSource> CreateSetter<TSource>(MemberInfo member, object target)
        {
            switch (member)
            {
                case PropertyInfo property:
                    {
                        MethodInfo setter = property.GetSetMethod(true);
                        if (setter == null || property.GetIndexParameters().Length != 0)
                        {
                            return null;
                        }

                        if (setter.GetParameters().Length == 1 && setter.GetParameters()[0].ParameterType == typeof(TSource))
                        {
                            try
                            {
                                return BindSetter<TSource>(setter, target);
                            }
                            catch (Exception)
                            {
                                // Fall through to the reflection fallback selected at initialization.
                            }
                        }

                        return CreateReflectionSetter<TSource>(property, target);
                    }
                case MethodInfo method:
                    if (method.GetParameters().Length != 1 || method.GetParameters()[0].ParameterType != typeof(TSource))
                    {
                        return null;
                    }

                    try
                    {
                        return BindSetter<TSource>(method, target);
                    }
                    catch (Exception)
                    {
                        // Fall through to the reflection fallback selected at initialization.
                        return CreateReflectionSetter<TSource>(method, target);
                    }
                case FieldInfo field:
                    return CreateFieldSetter<TSource>(field, target);
                default:
                    return null;
            }
        }

        /// <summary>
        /// Binds a method as a closed or static typed getter delegate.
        /// </summary>
        private static Func<TSource> BindGetter<TSource>(MethodInfo method, object target)
        {
            return method.IsStatic
                ? (Func<TSource>)method.CreateDelegate(typeof(Func<TSource>))
                : (Func<TSource>)method.CreateDelegate(typeof(Func<TSource>), target);
        }

        /// <summary>
        /// Binds a method as a closed or static typed setter delegate.
        /// </summary>
        private static Action<TSource> BindSetter<TSource>(MethodInfo method, object target)
        {
            return method.IsStatic
                ? (Action<TSource>)method.CreateDelegate(typeof(Action<TSource>))
                : (Action<TSource>)method.CreateDelegate(typeof(Action<TSource>), target);
        }

        /// <summary>
        /// Builds an expression field getter when available and otherwise caches reflection.
        /// </summary>
        private static Func<TSource> CreateFieldGetter<TSource>(FieldInfo field, object target)
        {
#if !ENABLE_IL2CPP
            try
            {
                Expression fieldExpression = CreateFieldExpression(field, target);
                Type delegateType = typeof(Func<>).MakeGenericType(typeof(TSource));
                return (Func<TSource>)Expression.Lambda(delegateType, fieldExpression).Compile();
            }
            catch (Exception)
            {
                // Expression compilation is an optional optimization; use reflection below.
            }
#endif
            return () => (TSource)field.GetValue(target);
        }

        /// <summary>
        /// Builds an expression field setter when available and otherwise caches reflection.
        /// </summary>
        private static Action<TSource> CreateFieldSetter<TSource>(FieldInfo field, object target)
        {
            if (field.IsInitOnly)
            {
                return null;
            }

#if !ENABLE_IL2CPP
            try
            {
                ParameterExpression parameter = Expression.Parameter(typeof(TSource), "value");
                Expression fieldExpression = CreateFieldExpression(field, target);
                BinaryExpression assignment = Expression.Assign(fieldExpression, parameter);
                Type delegateType = typeof(Action<>).MakeGenericType(typeof(TSource));
                return (Action<TSource>)Expression.Lambda(delegateType, assignment, parameter).Compile();
            }
            catch (Exception)
            {
                // Expression compilation is an optional optimization; use reflection below.
            }
#endif
            return value => field.SetValue(target, value);
        }

        /// <summary>
        /// Creates a closed field expression for static or instance storage.
        /// </summary>
        private static Expression CreateFieldExpression(FieldInfo field, object target)
        {
            if (field.IsStatic)
            {
                return Expression.Field(null, field);
            }

            Expression instance = Expression.Convert(
                Expression.Constant(target),
                field.DeclaringType);
            return Expression.Field(instance, field);
        }

        /// <summary>
        /// Creates the explicit reflection getter fallback.
        /// </summary>
        private static Func<TSource> CreateReflectionGetter<TSource>(MemberInfo member, object target)
        {
            switch (member)
            {
                case PropertyInfo property:
                    return () => (TSource)property.GetValue(target, null);
                case MethodInfo method:
                    return () => (TSource)method.Invoke(target, EmptyArguments);
                default:
                    throw new NotSupportedException($"Member '{member.Name}' does not support reflection reads.");
            }
        }

        /// <summary>
        /// Creates the explicit reflection setter fallback.
        /// </summary>
        private static Action<TSource> CreateReflectionSetter<TSource>(MemberInfo member, object target)
        {
            switch (member)
            {
                case PropertyInfo property:
                    return value => property.SetValue(target, value, null);
                case MethodInfo method:
                    return value => method.Invoke(target, new object[] { value });
                default:
                    throw new NotSupportedException($"Member '{member.Name}' does not support reflection writes.");
            }
        }
    }
}
