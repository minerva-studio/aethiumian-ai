using Amlos.AI.Variables;
using System;
using UnityEngine;

namespace Amlos.AI.Nodes
{
    [Serializable]
    [NodeTip("let Behaviour Tree wait for given time")]
    public sealed class Wait : Action
    {
        public enum Mode
        {
            realTime,
            frame
        }

        public Mode mode;
        [Numeric]
        public VariableField time;
        private float currentTime;

        public override void Awake()
        {
            currentTime = 0;
        }

        public override void FixedUpdate()
        {
            switch (mode)
            {
                case Mode.realTime:
                    currentTime += Time.fixedDeltaTime;
                    if (currentTime > time.NumericValue)
                    {
                        //Debug.Log("Call End");
                        End(true);
                    }
                    break;
                case Mode.frame:
                    currentTime++;

                    if (currentTime > time.NumericValue)
                    {
                        End(true);
                    }
                    break;
                default:
                    break;
            }
        }

    }
}