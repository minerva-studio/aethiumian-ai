using System;
using UnityEngine;

namespace Amlos.AI
{
    [NodeTip("Position of Entity (2D)")]
    [Serializable]
    public class Position2D : ComparableDetermine<Vector2>
    {
        public override Vector2 GetValue()
        {
            return (Vector2)transform.position;
        }
    }
}