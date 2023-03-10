using Amlos.AI.Variables;
using System;
using UnityEngine;

namespace Amlos.AI.Nodes
{
    [NodeTip("Make Object Stay")]
    [Serializable]
    public sealed class Idle : Action
    {
        public VariableField<float> time;
        public VariableField<float> strength;
        float currentTime;
        Rigidbody2D rb;

        public override void BeforeExecute()
        {
            rb = gameObject.GetComponent<Rigidbody2D>();
            currentTime = 0;
        }

        public override void FixedUpdate()
        {
            if (!rb)
            {
                End(true);
                return;
            }
            currentTime += Time.fixedDeltaTime;
            float p = 1 - currentTime / time;
            if (p <= 0)
            {
                rb.velocity = Vector2.zero;
                End(true);
                return;
            }

            float strength = this.strength;
            strength = Mathf.Max(0, strength);
            strength = Mathf.Min(1, strength);
            rb.velocity = Vector2.Lerp(rb.velocity, Vector2.zero, strength);
        }
        public override void Update() { }


        public enum IdleType
        {
            instant,
            time,
            speed,
        }
    }
}