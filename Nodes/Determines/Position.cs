using System;
using UnityEngine;

namespace Amlos.AI.Nodes
{

    [NodeTip("Position of Entity")]
    [Serializable]
    public sealed class Position : ComparableDetermine<Vector3>
    {
        public override Vector3 GetValue()
        {
            return transform.position;
        }
    }
}