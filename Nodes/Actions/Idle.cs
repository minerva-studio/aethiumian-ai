using Minerva.Module;
using System;
using UnityEngine;

namespace Amlos.AI
{

    [NodeTip("Stop movement")]
    [Serializable]
    public class Idle : Action
    {
        public IdleType idleType;
        [DisplayIf(nameof(idleType), IdleType.speed)] public float speed;
        [DisplayIf(nameof(idleType), IdleType.speed)] public float velocityErrorBound;
        [DisplayIf(nameof(idleType), IdleType.time)] public float time;


        Vector2 initVelocity;
        float initTime;
        float currentTime;


        public override void BeforeExecute()
        {
            Rigidbody2D rb = gameObject.GetComponent<Rigidbody2D>();
            if (rb) initVelocity = rb.velocity;
            initTime = 0;
            currentTime = 0;
        }

        public override void FixedUpdate()
        {
            Rigidbody2D rb = gameObject.GetComponent<Rigidbody2D>();
            currentTime += Time.fixedDeltaTime;
            if (!rb)
            {
                End(true);
            }
            switch (idleType)
            {
                case IdleType.instant:
                    rb.velocity = Vector2.zero;
                    End(true);
                    break;
                case IdleType.time:
                    float p = 1 - (currentTime - initTime) / time;
                    if (p <= 0)
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
                    break;
            }
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