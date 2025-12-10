using Amlos.AI.Variables;
using UnityEngine;

namespace Amlos.AI.Nodes
{
    /// <summary>
    /// Fixed jump towards a target position with a specified height and duration.
    /// </summary>
    /// <remarks>
    /// Thanks to https://github.com/DuncanZh
    /// </remarks>
    [NodeTip("Fixed jump towards a target position with a specified height and duration")]
    public class FixedJump : Action
    {
        [Numeric]
        [Readable]
        public VariableField jumpHeight = new(30f);
        [Numeric]
        [Readable]
        public VariableField jumpDuration = new(2f);
        [Constraint(VariableType.Vector2, VariableType.Vector3, VariableType.UnityObject)]
        [Readable]
        public VariableField target;

        private float t = 0f;
        private Vector2 start;
        private Vector2 end;
        private float _jumpHeight;
        private float _jumpDuration;
        private Rigidbody2D rb;


        public override void Start()
        {
            if (target.IsNull)
            {
                End(false);
            }
            rb = transform.GetComponent<Rigidbody2D>();
            start = transform.position;
            end = target.PositionValue;
            _jumpDuration = jumpDuration.NumericValue;
            _jumpHeight = jumpHeight.NumericValue;
        }

        public override void FixedUpdate()
        {
            t += Time.fixedDeltaTime;

            float vx = (end.x - start.x) / _jumpDuration;
            float v0 = 2 * _jumpHeight / _jumpDuration;
            float vy = v0 - (2 * v0 / _jumpDuration) * t;
            rb.linearVelocity = new Vector2(vx, vy);

            if (t >= _jumpDuration)
            {
                rb.linearVelocityX = 0;
                End(true);
                return;
            }
        }
    }
}