using Amlos.AI.Variables;
using System;
using UnityEngine;

namespace Amlos.AI.Nodes
{
    [Serializable]
    [NodeTip("let Behaviour Tree wait for given time")]
    public sealed class Wait : Flow
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
        private float duration;

        public override void Initialize()
        {
            currentTime = 0;
        }

        public override State Execute()
        {
            duration = time.NumericValue;
            if (duration <= 0)
            {
                return State.Success;
            }

            switch (mode)
            {
                case Mode.realTime:
                    currentTime += Time.deltaTime;
                    break;
                case Mode.frame:
                    currentTime++;
                    break;
                default:
                    return State.Failed;
            }

            if (currentTime >= duration)
            {
                return State.Success;
            }

            return State.WaitUntilNextUpdate;
        }

        protected override void OnStop()
        {
            currentTime = 0;
        }
    }
}