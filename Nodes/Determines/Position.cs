using System;
using UnityEngine;

namespace Amlos.AI.Nodes
{

    [NodeTip("Position of game object this AI is at")]
    [Serializable]
    public sealed class Position : ComparableDetermine<Vector3>
    {
        public override Vector3 GetValue()
        {
            return transform.position;
        }
    }
}