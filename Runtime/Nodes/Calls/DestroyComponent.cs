using Aethiumian.AI.References;
using System;
using UnityEngine;

namespace Aethiumian.AI.Nodes
{
    [NodeTip("Destory an attached component")]
    [System.Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Amlos.AI.Nodes", "Aethiumian-AI")]
    public class DestroyComponent : Call
    {
        public TypeReference<Component> componentReference;

        public override State Execute()
        {
            Type type = componentReference;
            Component component = gameObject.GetComponent(type);
            UnityEngine.Object.Destroy(component);
            return State.Success;
        }
    }
}