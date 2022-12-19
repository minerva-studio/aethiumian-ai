using System;
using UnityEngine;

namespace Amlos.AI
{
    [NodeTip("Destory an attached component")]
    public class DestroyComponent : Call
    {
        public ComponentReference componentReference;

        public override void Execute()
        {
            Type type = componentReference;
            Component component = gameObject.GetComponent(type);
            UnityEngine.Object.Destroy(component);
            End(true);
        }
    }
}