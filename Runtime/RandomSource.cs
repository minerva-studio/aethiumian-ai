using Aethiumian.AI.Accessors;
using Aethiumian.AI.Nodes;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Aethiumian.AI.Randomization
{
    /// <summary>
    /// Runtime random source contract consumed by AI nodes.
    /// </summary>
    public interface IAIRandomSource
    {
        int NextInt(int maxExclusive);
        int NextInt(int minInclusive, int maxExclusive);
        float NextFloat();
        float NextFloat(float minInclusive, float maxExclusive);
    }

    /// <summary>
    /// Asset-level random source definition. Assets hold configuration; returned sources hold runtime state.
    /// </summary>
    public abstract class AIRandomSourceAsset : ScriptableObject
    {
        public abstract IAIRandomSource CreateSource(AIRandomSourceCreateContext context);
    }

    [Serializable]
    public sealed class AIRandomSourceReference : IDuplicable
    {
        public AIRandomSourceAsset asset;

        public bool HasAsset => asset;

        public object Duplicate()
        {
            return new AIRandomSourceReference { asset = asset };
        }
    }

    public readonly struct AIRandomSourceCreateContext
    {
        public readonly BehaviourTree Tree;
        public readonly AI AI;
        public readonly GameObject GameObject;
        public readonly MonoBehaviour Script;
        public readonly TreeNode Node;

        public bool HasNode => Node != null;

        public AIRandomSourceCreateContext(BehaviourTree tree, TreeNode node = null)
        {
            Tree = tree;
            AI = tree?.AIComponent;
            GameObject = tree?.gameObject;
            Script = tree?.Script;
            Node = node;
        }
    }

    /// <summary>
    /// Unity-backed fallback source used when no configured source is available.
    /// </summary>
    public sealed class UnityAIRandomSource : IAIRandomSource
    {
        public static readonly UnityAIRandomSource Shared = new();

        private UnityAIRandomSource()
        {
        }

        public int NextInt(int maxExclusive)
        {
            return UnityEngine.Random.Range(0, maxExclusive);
        }

        public int NextInt(int minInclusive, int maxExclusive)
        {
            return UnityEngine.Random.Range(minInclusive, maxExclusive);
        }

        public float NextFloat()
        {
            return UnityEngine.Random.value;
        }

        public float NextFloat(float minInclusive, float maxExclusive)
        {
            return UnityEngine.Random.Range(minInclusive, maxExclusive);
        }
    }

    /// <summary>
    /// Optional asset wrapper for explicitly selecting Unity random in AI settings.
    /// </summary>
    [CreateAssetMenu(fileName = "AI_Random_Unity", menuName = "Aethiumian AI/Random/Unity")]
    public sealed class UnityAIRandomSourceAsset : AIRandomSourceAsset
    {
        public override IAIRandomSource CreateSource(AIRandomSourceCreateContext context)
        {
            return UnityAIRandomSource.Shared;
        }
    }

    public sealed class AIRandomSourceResolver
    {
        private readonly BehaviourTree tree;
        private readonly Dictionary<TreeNode, IAIRandomSource> nodeSources = new();
        private IAIRandomSource treeSource;

        public AIRandomSourceResolver(BehaviourTree tree)
        {
            this.tree = tree ?? throw new ArgumentNullException(nameof(tree));
        }

        public IAIRandomSource TreeSource => treeSource ??= CreateTreeSource();

        public IAIRandomSource Resolve(TreeNode node, AIRandomSourceReference overrideSource)
        {
            AIRandomSourceAsset overrideAsset = overrideSource?.asset;
            if (!overrideAsset)
            {
                return TreeSource;
            }

            if (!nodeSources.TryGetValue(node, out var source) || source == null)
            {
                source = CreateFromAsset(overrideAsset, node) ?? TreeSource;
                nodeSources[node] = source;
            }

            return source;
        }

        private IAIRandomSource CreateTreeSource()
        {
            AIRandomSourceAsset asset = tree.Prototype ? tree.Prototype.randomSource : null;
            if (!asset)
            {
                asset = AISetting.Instance ? AISetting.Instance.globalRandomSource : null;
            }

            return CreateFromAsset(asset, null) ?? UnityAIRandomSource.Shared;
        }

        private IAIRandomSource CreateFromAsset(AIRandomSourceAsset asset, TreeNode node)
        {
            if (!asset)
            {
                return null;
            }

            try
            {
                return asset.CreateSource(new AIRandomSourceCreateContext(tree, node));
            }
            catch (Exception e)
            {
                Debug.LogException(e, tree.gameObject);
                return null;
            }
        }
    }

    public static class AIRandomSourceUtility
    {
        public static Vector2 NextVector2(this IAIRandomSource random, Vector2 min, Vector2 max)
        {
            return new Vector2(
                random.NextFloat(min.x, max.x),
                random.NextFloat(min.y, max.y));
        }

        public static Vector3 NextVector3(this IAIRandomSource random, Vector3 min, Vector3 max)
        {
            return new Vector3(
                random.NextFloat(min.x, max.x),
                random.NextFloat(min.y, max.y),
                random.NextFloat(min.z, max.z));
        }

        public static Vector2 NextVector2(this IAIRandomSource random, float xMaxExclusive, float yMaxExclusive)
        {
            return new Vector2(
                random.NextFloat(0f, xMaxExclusive),
                random.NextFloat(0f, yMaxExclusive));
        }

        public static Vector3 NextVector3(this IAIRandomSource random, float xMaxExclusive, float yMaxExclusive, float zMaxExclusive)
        {
            return new Vector3(
                random.NextFloat(0f, xMaxExclusive),
                random.NextFloat(0f, yMaxExclusive),
                random.NextFloat(0f, zMaxExclusive));
        }

        public static Vector2 NextInsideUnitCircle(this IAIRandomSource random)
        {
            float angle = random.NextFloat(0f, Mathf.PI * 2f);
            float radius = Mathf.Sqrt(random.NextFloat());
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }

        public static Vector2 NextUnitCircleDirection(this IAIRandomSource random)
        {
            float angle = random.NextFloat(0f, Mathf.PI * 2f);
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }
    }

    public static class AIWeightedRandom
    {
        public static T Pick<T>(IReadOnlyList<T> items, Func<T, int> getWeight, IAIRandomSource random)
        {
            if (items == null || items.Count == 0)
            {
                return default;
            }

            int total = 0;
            for (int i = 0; i < items.Count; i++)
            {
                total += Mathf.Max(0, getWeight(items[i]));
            }

            if (total <= 0)
            {
                return items[random.NextInt(items.Count)];
            }

            int roll = random.NextInt(total);
            for (int i = 0; i < items.Count; i++)
            {
                roll -= Mathf.Max(0, getWeight(items[i]));
                if (roll < 0)
                {
                    return items[i];
                }
            }

            return items[^1];
        }
    }
}
