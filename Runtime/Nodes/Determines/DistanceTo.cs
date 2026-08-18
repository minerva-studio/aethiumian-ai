using Aethiumian.AI.Variables;
using System;
using UnityEngine;

namespace Aethiumian.AI.Nodes
{
    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Amlos.AI.Nodes", "Aethiumian-AI")]
    [NodeTip("Reads the distance between the entity and a target.")]
    public sealed class DistanceTo : ComparableDetermine<float>
    {
        public enum DistanceType
        {
            /// <summary>
            /// the magitude of displacement
            /// </summary>
            euclidean,

            /// <summary>
            /// the sum of displacements of all coordination
            /// </summary>
            manhattan,

            /// <summary>
            /// the maximum of displacement of all coordination
            /// </summary>
            chebyshev,
        }

        public DistanceType distanceType;
        [Readable]
        public VariableReference<GameObject> @object;

        public override float GetValue()
        {
            if (!@object.HasValue) return float.PositiveInfinity;
            if (@object.TransformValue == null) return float.PositiveInfinity;
            return Distance(@object.TransformValue.position, distanceType);
        }

        public float Distance(Vector2 position, DistanceType distanceType)
        {
            Vector2 displacement = (Vector2)transform.position - position;
            switch (distanceType)
            {
                case DistanceType.manhattan:
                    return displacement.x + displacement.y;
                case DistanceType.chebyshev:
                    return Mathf.Max(displacement.x, displacement.y);
                case DistanceType.euclidean:
                default:
                    return displacement.magnitude;
            }
        }
    }
}
