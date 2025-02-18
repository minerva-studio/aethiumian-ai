using UnityEngine;

namespace Amlos.AI.Nodes
{
    [NodeTip("Get current moving direction of the entity")]
    public sealed class MovingDirection : ComparableDetermine<Vector2>
    {
        public bool usePhysics2D;
        private Rigidbody2D rb;
        private bool yield;
        private Vector2 lastPosition;

        public override void Initialize()
        {
            rb = transform.GetComponent<Rigidbody2D>();
            yield = false;
        }

        public override bool Yield
        {
            get
            {
                if (usePhysics2D) { return false; }
                lastPosition = transform.position;
                return yield = !yield;
            }
        }

        public override Vector2 GetValue()
        {
            if (usePhysics2D) return rb.linearVelocity;
            return (Vector2)transform.position - lastPosition;
        }
    }
}