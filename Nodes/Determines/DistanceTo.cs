using Amlos.AI.Variables;
using System;
using UnityEngine;

namespace Amlos.AI
{
    [AllowServiceCall]
    [Serializable]
    [NodeTip("Determine the distance between the entity to an object")]
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
        public VariableReference<GameObject> @object;

        public override float GetValue()
        {
            if (!@object.HasValue) throw InvalidNodeException.VariableIsRequired(nameof(@object));

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