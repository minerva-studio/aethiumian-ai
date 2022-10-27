using Minerva.Module;
using System;
using UnityEngine;

namespace Amlos.AI
{
    [Serializable]
    [NodeTip("let Behaviour Tree wait for given time")]
    public class Wait : Action
    {
        public enum Mode
        {
            realTime,
            frame
        }

        public Mode mode;
        public VariableField<float> time;
        private float currentTime;

        public override void BeforeExecute()
        {
            currentTime = 0;
        }

        public override void FixedUpdate()
        {
            switch (mode)
            {
                case Mode.realTime:
                    currentTime += Time.fixedDeltaTime;
                    if (currentTime > time)
                    {
                        Debug.Log("Call End");
                        End(true);
                    }
                    break;
                case Mode.frame:
                    currentTime++;

                    if (currentTime > time)
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