using Amlos.AI.Nodes;
using Amlos.AI.References;
using Amlos.AI.Variables;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Amlos.AI.Accessors
{
    /// <summary>
    /// Builds cached accessors for node reference and variable fields.
    /// </summary>
    /// <typeparam name="T">The node type to build accessors for.</typeparam>
    internal static class NodeAccessorBuilder<T> where T : TreeNode
    {
        private const BindingFlags FieldFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly;

        /// <summary>
        /// Creates a <see cref="NodeAccessor{T}"/> with cached reflection accessors.
        /// </summary>
        /// <returns>A cached <see cref="NodeAccessor{T}"/> instance.</returns>
        /// <remarks>Exceptions: none.</remarks>
        public static NodeAccessor<T> Create()
        {
            List<NodeReferenceAccessor> nodeReferenceAccessors = new();
            List<NodeReferenceCollectionAccessor> nodeReferenceCollectionAccessors = new();
            List<VariableAccessor> variableAccessors = new();
            List<VariableCollectionAccessor> variableCollectionAccessors = new();

            foreach (FieldInfo field in GetAllFields(typeof(T)))
            {
                if (TryAddNodeReferenceAccessor(field, nodeReferenceAccessors, nodeReferenceCollectionAccessors))
                {
                    continue;
                }

                if (TryAddVariableAccessor(field, variableAccessors, variableCollectionAccessors))
                {
                    continue;
                }
            }

            return new NodeAccessor<T>(
                nodeReferenceAccessors.ToArray(),
                nodeReferenceCollectionAccessors.ToArray(),
                variableAccessors.ToArray(),
                variableCollectionAccessors.ToArray());
        }

        /// <summary>
        /// Enumerates all fields in the node type hierarchy.
        /// </summary>
        /// <param name="type">The node type to inspect.</param>
        /// <returns>An enumerable of all relevant <see cref="FieldInfo"/> instances.</returns>
        /// <remarks>Exceptions: none.</remarks>
        private static IEnumerable<FieldInfo> GetAllFields(Type type)
        {
            for (Type current = type; current != null && current != typeof(object); current = current.BaseType)
            {
                foreach (FieldInfo field in current.GetFields(FieldFlags))
                {
                    yield return field;
                }
            }
        }

        /// <summary>
        /// Attempts to add node reference accessors for the supplied field.
        /// </summary>
        /// <param name="field">The field metadata to inspect.</param>
        /// <param name="nodeReferenceAccessors">The list to receive node reference accessors.</param>
        /// <param name="nodeReferenceCollectionAccessors">The list to receive node reference collection accessors.</param>
        /// <returns><see langword="true"/> when an accessor was added; otherwise <see langword="false"/>.</returns>
        /// <remarks>Exceptions: none.</remarks>
        private static bool TryAddNodeReferenceAccessor(
            FieldInfo field,
            ICollection<NodeReferenceAccessor> nodeReferenceAccessors,
            ICollection<NodeReferenceCollectionAccessor> nodeReferenceCollectionAccessors)
        {
            if (typeof(INodeReference).IsAssignableFrom(field.FieldType))
            {
                nodeReferenceAccessors.Add(new NodeReferenceAccessor(
                    field.Name,
                    field.FieldType,
                    CreateNodeReferenceGetter(field),
                    CreateNodeReferenceSetter(field)));
                return true;
            }

            if (TryGetCollectionElementType(field.FieldType, out Type elementType)
                && typeof(INodeReference).IsAssignableFrom(elementType))
            {
                nodeReferenceCollectionAccessors.Add(new NodeReferenceCollectionAccessor(
                    field.Name,
                    field.FieldType,
                    elementType,
                    CreateListGetter(field),
                    CreateListSetter(field)));
                return true;
            }

            return false;
        }

        /// <summary>
        /// Attempts to add variable accessors for the supplied field.
        /// </summary>
        /// <param name="field">The field metadata to inspect.</param>
        /// <param name="variableAccessors">The list to receive variable accessors.</param>
        /// <param name="variableCollectionAccessors">The list to receive variable collection accessors.</param>
        /// <returns><see langword="true"/> when an accessor was added; otherwise <see langword="false"/>.</returns>
        /// <remarks>Exceptions: none.</remarks>
        private static bool TryAddVariableAccessor(
            FieldInfo field,
            ICollection<VariableAccessor> variableAccessors,
            ICollection<VariableCollectionAccessor> variableCollectionAccessors)
        {
            if (typeof(VariableBase).IsAssignableFrom(field.FieldType))
            {
                variableAccessors.Add(new VariableAccessor(
                    field.Name,
                    field.FieldType,
                    CreateVariableGetter(field)));
                return true;
            }

            if (TryGetCollectionElementType(field.FieldType, out Type elementType)
                && typeof(VariableBase).IsAssignableFrom(elementType))
            {
                variableCollectionAccessors.Add(new VariableCollectionAccessor(
                    field.Name,
                    elementType,
                    CreateListGetter(field)));
                return true;
            }

            return false;
        }

        /// <summary>
        /// Attempts to get the element type from a collection field.
        /// </summary>
        /// <param name="fieldType">The field type to inspect.</param>
        /// <param name="elementType">The element type when the field is a supported collection.</param>
        /// <returns><see langword="true"/> if an element type was found; otherwise <see langword="false"/>.</returns>
        /// <remarks>Exceptions: none.</remarks>
        private static bool TryGetCollectionElementType(Type fieldType, out Type elementType)
        {
            if (fieldType.IsArray)
            {
                elementType = fieldType.GetElementType();
                return elementType != null;
            }

            if (typeof(IList).IsAssignableFrom(fieldType) && fieldType.IsGenericType)
            {
                elementType = fieldType.GetGenericArguments().FirstOrDefault();
                return elementType != null;
            }

            elementType = null;
            return false;
        }

        /// <summary>
        /// Creates a cached getter for a node reference field.
        /// </summary>
        /// <param name="field">The field metadata to access.</param>
        /// <returns>A cached getter delegate.</returns>
        /// <remarks>Exceptions: none.</remarks>
        private static Func<TreeNode, INodeReference> CreateNodeReferenceGetter(FieldInfo field)
        {
            ParameterExpression instanceParameter = Expression.Parameter(typeof(TreeNode), "node");
            Expression instance = Expression.Convert(instanceParameter, field.DeclaringType);
            Expression fieldExpression = Expression.Field(instance, field);
            Expression convert = Expression.Convert(fieldExpression, typeof(INodeReference));
            return Expression.Lambda<Func<TreeNode, INodeReference>>(convert, instanceParameter).Compile();
        }

        /// <summary>
        /// Creates a cached setter for a node reference field.
        /// </summary>
        /// <param name="field">The field metadata to access.</param>
        /// <returns>A cached setter delegate.</returns>
        /// <remarks>Exceptions: none.</remarks>
        private static Action<TreeNode, INodeReference> CreateNodeReferenceSetter(FieldInfo field)
        {
            ParameterExpression instanceParameter = Expression.Parameter(typeof(TreeNode), "node");
            ParameterExpression valueParameter = Expression.Parameter(typeof(INodeReference), "value");
            Expression instance = Expression.Convert(instanceParameter, field.DeclaringType);
            Expression value = Expression.Convert(valueParameter, field.FieldType);
            Expression assign = Expression.Assign(Expression.Field(instance, field), value);
            return Expression.Lambda<Action<TreeNode, INodeReference>>(assign, instanceParameter, valueParameter).Compile();
        }

        /// <summary>
        /// Creates a cached getter for a variable field.
        /// </summary>
        /// <param name="field">The field metadata to access.</param>
        /// <returns>A cached getter delegate.</returns>
        /// <remarks>Exceptions: none.</remarks>
        private static Func<TreeNode, VariableBase> CreateVariableGetter(FieldInfo field)
        {
            ParameterExpression instanceParameter = Expression.Parameter(typeof(TreeNode), "node");
            Expression instance = Expression.Convert(instanceParameter, field.DeclaringType);
            Expression fieldExpression = Expression.Field(instance, field);
            Expression convert = Expression.Convert(fieldExpression, typeof(VariableBase));
            return Expression.Lambda<Func<TreeNode, VariableBase>>(convert, instanceParameter).Compile();
        }

        /// <summary>
        /// Creates a cached getter for a collection field.
        /// </summary>
        /// <param name="field">The field metadata to access.</param>
        /// <returns>A cached getter delegate.</returns>
        /// <remarks>Exceptions: none.</remarks>
        private static Func<TreeNode, IList> CreateListGetter(FieldInfo field)
        {
            ParameterExpression instanceParameter = Expression.Parameter(typeof(TreeNode), "node");
            Expression instance = Expression.Convert(instanceParameter, field.DeclaringType);
            Expression fieldExpression = Expression.Field(instance, field);
            Expression convert = Expression.Convert(fieldExpression, typeof(IList));
            return Expression.Lambda<Func<TreeNode, IList>>(convert, instanceParameter).Compile();
        }

        /// <summary>
        /// Creates a cached setter for a collection field.
        /// </summary>
        /// <param name="field">The field metadata to access.</param>
        /// <returns>A cached setter delegate.</returns>
        /// <remarks>Exceptions: none.</remarks>
        private static Action<TreeNode, IList> CreateListSetter(FieldInfo field)
        {
            ParameterExpression instanceParameter = Expression.Parameter(typeof(TreeNode), "node");
            ParameterExpression valueParameter = Expression.Parameter(typeof(IList), "value");
            Expression instance = Expression.Convert(instanceParameter, field.DeclaringType);
            Expression value = Expression.Convert(valueParameter, field.FieldType);
            Expression assign = Expression.Assign(Expression.Field(instance, field), value);
            return Expression.Lambda<Action<TreeNode, IList>>(assign, instanceParameter, valueParameter).Compile();
        }
    }
}
