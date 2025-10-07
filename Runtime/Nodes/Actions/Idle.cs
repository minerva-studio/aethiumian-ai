﻿using Amlos.AI.Variables;
using System;
using UnityEngine;

namespace Amlos.AI.Nodes
{
    [NodeTip("Make Object Stay")]
    [Serializable]
    public sealed class Idle : Action
    {
        [Readable]
        public VariableField<float> time;
        [Readable]
        public VariableField<float> strength;
        float currentTime;
        Rigidbody2D rb;

        public override void Awake()
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
#if UNITY_6000_0_OR_NEWER
                rb.linearVelocity = Vector2.zero;
#else
                rb.velocity = Vector2.zero;
#endif
                End(true);
                return;
            }

            float strength = this.strength;
            strength = Mathf.Max(0, strength);
            strength = Mathf.Min(1, strength);
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, Vector2.zero, strength);
#else
            rb.velocity = Vector2.Lerp(rb.velocity, Vector2.zero, strength);
#endif
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