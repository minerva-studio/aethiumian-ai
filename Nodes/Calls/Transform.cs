using Minerva.Module;
using System;
using UnityEngine;

namespace Amlos.AI
{
    [Serializable]
    [NodeTip("Set the property of transform")]
    public class SetTransform : Call
    {
        public bool setPosition;
        [DisplayIf(nameof(setPosition))] public VariableField<Vector3> position;
        public bool setScale;
        [DisplayIf(nameof(setScale))] public VariableField<Vector3> localScale;
        public bool setRotation;
        [DisplayIf(nameof(setRotation))] public VariableField<Vector3> eulerAngles;

        public override void Execute()
        {
            if (setPosition)
            {
                transform.position = position;
            }
            if (setScale)
            {
                transform.localScale = localScale;
            }
            if (setRotation)
            {
                transform.eulerAngles = eulerAngles;
            }
            End(setPosition || setScale || setRotation);
        }
    }
}