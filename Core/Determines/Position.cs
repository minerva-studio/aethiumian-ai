using System;
using UnityEngine;

namespace Amlos.AI
{

    [NodeTip("Position of Entity")]
    [Serializable]
    public class Position : ComparableDetermine<Vector3>
    {
        public override Vector3 GetValue()
        {
            return transform.position;
        }
    }
}