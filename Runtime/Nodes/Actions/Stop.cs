using Minerva.Module;
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
        Rigidbody2D rb;

        public Vector2 Velocity
        {
            get
            {
#if UNITY_6000_0_OR_NEWER
                return rb.linearVelocity;
#else
                return rb.velocity;
#endif
            }
            set
            {
#if UNITY_6000_0_OR_NEWER
                rb.linearVelocity = value;
#else
                rb.velocity = value;
#endif
            }
        }


        public override void Awake()
        {
            rb = gameObject.GetComponent<Rigidbody2D>();
            if (rb) initVelocity = Velocity;
            initTime = 0;
            currentTime = 0;
        }

        public override void FixedUpdate()
        {
            currentTime += Time.fixedDeltaTime;
            if (!rb)
            {
                rb = gameObject.GetComponent<Rigidbody2D>();
                if (!rb)
                {
                    End(true);
                    return;
                }
            }
            switch (idleType)
            {
                case IdleType.instant:
                    Velocity = Vector2.zero;
                    End(true);
                    return;
                case IdleType.time:
                    float p = 1 - (currentTime - initTime) / time;
                    if (currentTime > time)
                    {
                        Velocity = Vector2.zero;
                        End(true);
                        return;
                    }
                    Velocity = initVelocity * p;
                    break;
                case IdleType.speed:
                    Vector2 reverse = -Velocity.normalized * (1 - 1 / speed);
                    Velocity += reverse;
                    if (Velocity.magnitude < velocityErrorBound)
                    {
                        Velocity = Vector2.zero;
                        End(true);
                        return;
                    }
                    break;
                default:
                    Debug.LogError("?");
                    break;
            }
        }


        public enum IdleType
        {
            instant,
            time,
            speed,
        }
    }
}