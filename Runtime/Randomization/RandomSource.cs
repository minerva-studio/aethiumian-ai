using Aethiumian.AI.Nodes;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Aethiumian.AI.Randomization
{
    /// <summary>
    /// Runtime random source contract consumed by AI nodes.
    /// </summary>
    public interface IRandomSource
    {
        int NextInt(int maxExclusive);
        int NextInt(int minInclusive, int maxExclusive);
        float NextFloat();
        float NextFloat(float minInclusive, float maxExclusive);
    }

    /// <summary>
    /// Resolves configured random source bindings into runtime random source instances.
    /// </summary>
    public sealed class RandomSourceResolver
    {
        private static int nextTreeInstanceId;
        private static readonly Dictionary<RandomSourceAsset, IRandomSource> globalSources = new();
        private static readonly Dictionary<TreeStaticKey, IRandomSource> staticTreeSources = new();
        private static readonly Dictionary<NodeStaticKey, IRandomSource> staticNodeSources = new();

        private readonly BehaviourTree tree;
        private readonly int treeInstanceId;
        private readonly Dictionary<RandomSourceAsset, IRandomSource> treeEntrySources = new();
        private readonly Dictionary<RandomSourceAsset, IRandomSource> treeLocalSources = new();
        private readonly Dictionary<NodeSourceKey, IRandomSource> nodeEntrySources = new();
        private readonly Dictionary<NodeSourceKey, IRandomSource> nodeLocalSources = new();
        private readonly Dictionary<TreeNode, int> nodeActivationIds = new(ReferenceEqualityComparer<TreeNode>.Instance);

        private IRandomSource treeSource;
        private int treeRunId;
        private int nextNodeActivationId;

        public RandomSourceResolver(BehaviourTree tree)
        {
            this.tree = tree ?? throw new ArgumentNullException(nameof(tree));
            treeInstanceId = unchecked(++nextTreeInstanceId);
        }

        public IRandomSource TreeSource => treeSource ??= ResolveTreeOrDefaultSource();

        /// <summary>
        /// Clears random sources whose lifetime is tied to a single tree run.
        /// </summary>
        public void BeginTreeRun()
        {
            treeRunId = unchecked(treeRunId + 1);
            treeSource = null;
            treeEntrySources.Clear();
            nodeEntrySources.Clear();
            nodeActivationIds.Clear();
            nextNodeActivationId = 0;
        }

        /// <summary>
        /// Releases random sources whose lifetime is tied to the current activation of a node.
        /// </summary>
        /// <param name="node">The runtime node leaving the execution stack.</param>
        public void ReleaseNodeActivation(TreeNode node)
        {
            if (node == null)
            {
                return;
            }

            nodeActivationIds.Remove(node);
            RemoveNodeEntrySources(node);
        }

        /// <summary>
        /// Resolves the random source visible from a node, falling back through tree and project defaults.
        /// </summary>
        /// <param name="node">The runtime node requesting randomness.</param>
        /// <returns>The resolved random source instance.</returns>
        public IRandomSource Resolve(TreeNode node)
        {
            return Resolve(node, default);
        }

        /// <summary>
        /// Resolves a node override random source, falling back through tree and project defaults when not set.
        /// </summary>
        /// <param name="node">The runtime node requesting randomness.</param>
        /// <param name="overrideSource">The node-level random source binding.</param>
        /// <returns>The resolved random source instance.</returns>
        public IRandomSource Resolve(TreeNode node, RandomSourceBinding overrideSource)
        {
            if (!overrideSource.HasSource)
            {
                return TreeSource;
            }

            return ResolveBinding(node, overrideSource, true, null) ?? TreeSource;
        }

        private IRandomSource ResolveTreeOrDefaultSource()
        {
            if (tree.Prototype && tree.Prototype.randomSource.HasSource)
            {
                return ResolveBinding(null, tree.Prototype.randomSource, false, UnityRandomSource.Shared);
            }

            AISetting settings = AISetting.Instance;
            if (settings && settings.defaultRandomSource.HasSource)
            {
                return ResolveBinding(null, settings.defaultRandomSource, false, UnityRandomSource.Shared);
            }

            return UnityRandomSource.Shared;
        }

        private IRandomSource ResolveBinding(TreeNode node, RandomSourceBinding binding, bool isNodeBinding, IRandomSource fallback)
        {
            RandomSourceAsset asset = binding.source;
            if (!asset)
            {
                return fallback;
            }

            RandomSourceScope actualScope = asset.NormalizeScope(binding.scope);
            return actualScope switch
            {
                RandomSourceScope.Entry => ResolveEntrySource(node, isNodeBinding, asset, actualScope, fallback),
                RandomSourceScope.Local => ResolveLocalSource(node, isNodeBinding, asset, actualScope, fallback),
                RandomSourceScope.Static => ResolveStaticSource(node, isNodeBinding, asset, actualScope, fallback),
                RandomSourceScope.Global => ResolveGlobalSource(asset, actualScope, fallback),
                _ => fallback,
            };
        }

        private IRandomSource ResolveEntrySource(TreeNode node, bool isNodeBinding, RandomSourceAsset asset, RandomSourceScope scope, IRandomSource fallback)
        {
            if (isNodeBinding && node != null)
            {
                NodeSourceKey key = new(node, asset);
                if (!nodeEntrySources.TryGetValue(key, out IRandomSource source) || source == null)
                {
                    source = CreateFromAsset(asset, scope, CreateNodeEntrySeedSalt(node, asset), fallback);
                    nodeEntrySources[key] = source;
                }

                return source;
            }

            return GetOrCreate(treeEntrySources, asset, scope, CreateTreeEntrySeedSalt(asset), fallback);
        }

        private IRandomSource ResolveLocalSource(TreeNode node, bool isNodeBinding, RandomSourceAsset asset, RandomSourceScope scope, IRandomSource fallback)
        {
            if (isNodeBinding && node != null)
            {
                NodeSourceKey key = new(node, asset);
                if (!nodeLocalSources.TryGetValue(key, out IRandomSource source) || source == null)
                {
                    source = CreateFromAsset(asset, scope, CreateNodeLocalSeedSalt(node, asset), fallback);
                    nodeLocalSources[key] = source;
                }

                return source;
            }

            return GetOrCreate(treeLocalSources, asset, scope, CreateTreeLocalSeedSalt(asset), fallback);
        }

        private IRandomSource ResolveStaticSource(TreeNode node, bool isNodeBinding, RandomSourceAsset asset, RandomSourceScope scope, IRandomSource fallback)
        {
            if (isNodeBinding && node != null)
            {
                NodeStaticKey key = new(tree.Prototype, GetPrototypeNodeUUID(node), asset);
                if (!staticNodeSources.TryGetValue(key, out IRandomSource source) || source == null)
                {
                    source = CreateFromAsset(asset, scope, CreateNodeStaticSeedSalt(node, asset), fallback);
                    staticNodeSources[key] = source;
                }

                return source;
            }

            TreeStaticKey treeKey = new(tree.Prototype, asset);
            if (!staticTreeSources.TryGetValue(treeKey, out IRandomSource treeStaticSource) || treeStaticSource == null)
            {
                treeStaticSource = CreateFromAsset(asset, scope, CreateTreeStaticSeedSalt(asset), fallback);
                staticTreeSources[treeKey] = treeStaticSource;
            }

            return treeStaticSource;
        }

        private IRandomSource ResolveGlobalSource(RandomSourceAsset asset, RandomSourceScope scope, IRandomSource fallback)
        {
            if (!globalSources.TryGetValue(asset, out IRandomSource source) || source == null)
            {
                source = CreateFromAsset(asset, scope, CreateGlobalSeedSalt(asset), fallback);
                globalSources[asset] = source;
            }

            return source;
        }

        private IRandomSource GetOrCreate(Dictionary<RandomSourceAsset, IRandomSource> cache, RandomSourceAsset asset, RandomSourceScope scope, int seedSalt, IRandomSource fallback)
        {
            if (!cache.TryGetValue(asset, out IRandomSource source) || source == null)
            {
                source = CreateFromAsset(asset, scope, seedSalt, fallback);
                cache[asset] = source;
            }

            return source;
        }

        private IRandomSource CreateFromAsset(RandomSourceAsset asset, RandomSourceScope scope, int seedSalt, IRandomSource fallback)
        {
            if (!asset)
            {
                return fallback;
            }

            try
            {
                return asset.CreateSource(new RandomSourceCreateContext(scope, seedSalt)) ?? fallback;
            }
            catch (Exception e)
            {
                Debug.LogException(e, tree.gameObject);
                return fallback;
            }
        }

        private void RemoveNodeEntrySources(TreeNode node)
        {
            List<NodeSourceKey> keysToRemove = null;
            foreach (NodeSourceKey key in nodeEntrySources.Keys)
            {
                if (ReferenceEquals(key.Node, node))
                {
                    keysToRemove ??= new List<NodeSourceKey>();
                    keysToRemove.Add(key);
                }
            }

            if (keysToRemove == null)
            {
                return;
            }

            foreach (NodeSourceKey key in keysToRemove)
            {
                nodeEntrySources.Remove(key);
            }
        }

        private int CreateGlobalSeedSalt(RandomSourceAsset asset)
        {
            return HashSeed((int)RandomSourceScope.Global, GetUnityObjectId(asset));
        }

        private int CreateTreeEntrySeedSalt(RandomSourceAsset asset)
        {
            return HashSeed((int)RandomSourceScope.Entry, treeInstanceId, treeRunId, GetUnityObjectId(asset));
        }

        private int CreateTreeLocalSeedSalt(RandomSourceAsset asset)
        {
            return HashSeed((int)RandomSourceScope.Local, treeInstanceId, GetUnityObjectId(asset));
        }

        private int CreateTreeStaticSeedSalt(RandomSourceAsset asset)
        {
            return HashSeed((int)RandomSourceScope.Static, GetUnityObjectId(tree.Prototype), GetUnityObjectId(asset));
        }

        private int CreateNodeEntrySeedSalt(TreeNode node, RandomSourceAsset asset)
        {
            return HashSeed((int)RandomSourceScope.Entry, treeInstanceId, GetNodeId(node), GetNodeActivationId(node), GetUnityObjectId(asset));
        }

        private int CreateNodeLocalSeedSalt(TreeNode node, RandomSourceAsset asset)
        {
            return HashSeed((int)RandomSourceScope.Local, treeInstanceId, GetNodeId(node), GetUnityObjectId(asset));
        }

        private int CreateNodeStaticSeedSalt(TreeNode node, RandomSourceAsset asset)
        {
            return HashSeed((int)RandomSourceScope.Static, GetUnityObjectId(tree.Prototype), GetPrototypeNodeUUID(node).GetHashCode(), GetUnityObjectId(asset));
        }

        private int GetNodeActivationId(TreeNode node)
        {
            if (!nodeActivationIds.TryGetValue(node, out int activationId))
            {
                activationId = unchecked(++nextNodeActivationId);
                nodeActivationIds[node] = activationId;
            }

            return activationId;
        }

        private static int GetNodeId(TreeNode node)
        {
            return node != null ? node.UUID.GetHashCode() : 0;
        }

        private static UUID GetPrototypeNodeUUID(TreeNode node)
        {
            return node?.Prototype != null ? node.Prototype.UUID : node?.UUID ?? UUID.Empty;
        }

        private static int GetUnityObjectId(UnityEngine.Object unityObject)
        {
            return unityObject ? unityObject.GetInstanceID() : 0;
        }

        private static int HashSeed(params int[] values)
        {
            unchecked
            {
                int hash = 17;
                foreach (int value in values)
                {
                    hash = (hash * 397) ^ value;
                }

                return hash;
            }
        }

        private readonly struct NodeSourceKey : IEquatable<NodeSourceKey>
        {
            public readonly TreeNode Node;
            private readonly RandomSourceAsset source;

            public NodeSourceKey(TreeNode node, RandomSourceAsset source)
            {
                Node = node;
                this.source = source;
            }

            public bool Equals(NodeSourceKey other)
            {
                return ReferenceEquals(Node, other.Node) && source == other.source;
            }

            public override bool Equals(object obj)
            {
                return obj is NodeSourceKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashSeed(RuntimeHelpers.GetHashCode(Node), GetUnityObjectId(source));
            }
        }

        private readonly struct TreeStaticKey : IEquatable<TreeStaticKey>
        {
            private readonly BehaviourTreeData treeData;
            private readonly RandomSourceAsset source;

            public TreeStaticKey(BehaviourTreeData treeData, RandomSourceAsset source)
            {
                this.treeData = treeData;
                this.source = source;
            }

            public bool Equals(TreeStaticKey other)
            {
                return treeData == other.treeData && source == other.source;
            }

            public override bool Equals(object obj)
            {
                return obj is TreeStaticKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashSeed(GetUnityObjectId(treeData), GetUnityObjectId(source));
            }
        }

        private readonly struct NodeStaticKey : IEquatable<NodeStaticKey>
        {
            private readonly BehaviourTreeData treeData;
            private readonly UUID prototypeNodeUUID;
            private readonly RandomSourceAsset source;

            public NodeStaticKey(BehaviourTreeData treeData, UUID prototypeNodeUUID, RandomSourceAsset source)
            {
                this.treeData = treeData;
                this.prototypeNodeUUID = prototypeNodeUUID;
                this.source = source;
            }

            public bool Equals(NodeStaticKey other)
            {
                return treeData == other.treeData && prototypeNodeUUID == other.prototypeNodeUUID && source == other.source;
            }

            public override bool Equals(object obj)
            {
                return obj is NodeStaticKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashSeed(GetUnityObjectId(treeData), prototypeNodeUUID.GetHashCode(), GetUnityObjectId(source));
            }
        }

        private sealed class ReferenceEqualityComparer<T> : IEqualityComparer<T>
            where T : class
        {
            public static readonly ReferenceEqualityComparer<T> Instance = new();

            public bool Equals(T x, T y)
            {
                return ReferenceEquals(x, y);
            }

            public int GetHashCode(T obj)
            {
                return RuntimeHelpers.GetHashCode(obj);
            }
        }
    }
}
