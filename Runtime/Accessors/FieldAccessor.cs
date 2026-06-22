using Aethiumian.AI.References;
using Aethiumian.AI.Variables;
using Aethiumian.AI.Nodes;
using System;
using System.Collections;

namespace Aethiumian.AI.Accessors
{
    public interface IFieldAccessor
    {
        string Name { get; }
        Type FieldType { get; }
        object GetObject(object instance);
        void SetObject(object instance, object value);
    }

    public interface IFieldAccessor<TInstance, TField> : IFieldAccessor
    {
        TField Get(TInstance instance);
        void Set(TInstance instance, TField value);
    }

    public abstract class FieldAccessor<TInstance, TField> : IFieldAccessor<TInstance, TField>
    {
        public abstract string Name { get; }
        public abstract Type FieldType { get; }

        public abstract TField Get(TInstance instance);
        public abstract void Set(TInstance instance, TField value);

        public object GetObject(object instance)
        {
            return Get((TInstance)instance);
        }

        public void SetObject(object instance, object value)
        {
            Set((TInstance)instance, (TField)value);
        }
    }

    public interface INodeReferenceFieldAccessor : IFieldAccessor
    {
        INodeReference Get(TreeNode node);
        void Set(TreeNode node, INodeReference value);
    }

    public interface INodeReferenceCollectionFieldAccessor : IFieldAccessor
    {
        Type CollectionType { get; }
        Type ElementType { get; }
        IList Get(TreeNode node);
        void Set(TreeNode node, IList value);
    }

    public interface IVariableFieldAccessor : IFieldAccessor
    {
        IVariableField Get(TreeNode node);
        void Set(TreeNode node, IVariableField value);
    }

    public interface IVariableCollectionFieldAccessor : IFieldAccessor
    {
        Type CollectionType { get; }
        Type ElementType { get; }
        IList Get(TreeNode node);
        void Set(TreeNode node, IList value);
    }

    public abstract class NodeReferenceFieldAccessor<TNode> :
        FieldAccessor<TNode, INodeReference>,
        INodeReferenceFieldAccessor
        where TNode : TreeNode
    {
        INodeReference INodeReferenceFieldAccessor.Get(TreeNode node)
        {
            return Get((TNode)node);
        }

        void INodeReferenceFieldAccessor.Set(TreeNode node, INodeReference value)
        {
            Set((TNode)node, value);
        }
    }

    public abstract class NodeReferenceCollectionFieldAccessor<TNode> :
        FieldAccessor<TNode, IList>,
        INodeReferenceCollectionFieldAccessor
        where TNode : TreeNode
    {
        public abstract Type CollectionType { get; }
        public abstract Type ElementType { get; }

        public override Type FieldType => CollectionType;

        IList INodeReferenceCollectionFieldAccessor.Get(TreeNode node)
        {
            return Get((TNode)node);
        }

        void INodeReferenceCollectionFieldAccessor.Set(TreeNode node, IList value)
        {
            Set((TNode)node, value);
        }
    }

    public abstract class VariableFieldAccessor<TNode> :
        FieldAccessor<TNode, IVariableField>,
        IVariableFieldAccessor
        where TNode : TreeNode
    {
        IVariableField IVariableFieldAccessor.Get(TreeNode node)
        {
            return Get((TNode)node);
        }

        void IVariableFieldAccessor.Set(TreeNode node, IVariableField value)
        {
            Set((TNode)node, value);
        }
    }

    public abstract class VariableCollectionFieldAccessor<TNode> :
        FieldAccessor<TNode, IList>,
        IVariableCollectionFieldAccessor
        where TNode : TreeNode
    {
        public abstract Type CollectionType { get; }
        public abstract Type ElementType { get; }

        public override Type FieldType => CollectionType;

        IList IVariableCollectionFieldAccessor.Get(TreeNode node)
        {
            return Get((TNode)node);
        }

        void IVariableCollectionFieldAccessor.Set(TreeNode node, IList value)
        {
            Set((TNode)node, value);
        }
    }
}
