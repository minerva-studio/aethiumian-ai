﻿using Minerva.Module;
using System;
using UnityEngine;

namespace Amlos.AI.Nodes
{

    [NodeTip("Stop movement")]
    [Serializable]
    public sealed class Stop : Action
    {
        public IdleType idleType;
        [DisplayIf(nameof(idleType), IdleType.speed)] public float speed;
        [DisplayIf(nameof(idleType), IdleType.speed)] public float velocityErrorBound;
        [DisplayIf(nameof(idleType), IdleType.time)] public float time;


        Vector2 initVelocity;
        float initTime;
        float currentTime;


        public override void Awake()
        {
            Rigidbody2D rb = gameObject.GetComponent<Rigidbody2D>();
            if (rb) initVelocity = rb.velocity;
            initTime = 0;
            currentTime = 0;
            Debug.Log("Awake");
        }

        public override void FixedUpdate()
        {
            Rigidbody2D rb = gameObject.GetComponent<Rigidbody2D>();
            currentTime += Time.fixedDeltaTime;
            if (!rb)
            {
                End(true);
                return;
            }
            switch (idleType)
            {
                case IdleType.instant:
                    rb.velocity = Vector2.zero;
                    End(true);
                    return;
                case IdleType.time:
                    float p = 1 - (currentTime - initTime) / time;
                    if (currentTime > time)
                    {
                        rb.velocity = Vector2.zero;
                        End(true);
                        return;
                    }
                    rb.velocity = initVelocity * p;
                    break;
                case IdleType.speed:
                    Vector2 reverse = -rb.velocity.normalized * (1 - 1 / speed);
                    rb.velocity += reverse;
                    if (rb.velocity.magnitude < velocityErrorBound)
                    {
                        rb.velocity = Vector2.zero;
                        End(true);
                        return;
                    }
                    break;
                default:
                    Debug.LogError("?");
                    break;
            }
            Debug.Log(currentTime);
            Debug.Log("Running");
        }


        public enum IdleType
        {
            instant,
            time,
            speed,
        }
    }
}