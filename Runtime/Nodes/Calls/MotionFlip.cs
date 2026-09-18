using System;
using UnityEngine;

namespace Aethiumian.AI.Nodes
{
    /// <summary>Updates a local sprite's horizontal flip from actual horizontal motion.</summary>
    [Serializable]
    [NodeTip("Flip a SpriteRenderer from horizontal Rigidbody2D motion")]
    public sealed class MotionFlip : Call
    {
        private const float HorizontalMotionThreshold = 0.01f;

        [NonSerialized] private Rigidbody2D body;
        [NonSerialized] private SpriteRenderer spriteRenderer;

        public override void Initialize()
        {
            body = transform.GetComponent<Rigidbody2D>();
            spriteRenderer = transform.GetComponent<SpriteRenderer>();
        }

        public override State Execute()
        {
            if (!body || !spriteRenderer)
                return State.Failed;

            float horizontalVelocity = body.linearVelocity.x;
            if (Mathf.Abs(horizontalVelocity) > HorizontalMotionThreshold)
                spriteRenderer.flipX = horizontalVelocity < 0f;

            return State.Success;
        }
    }
}
