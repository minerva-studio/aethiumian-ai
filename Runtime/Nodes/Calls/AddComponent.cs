using Aethiumian.AI.References;
using System;
using UnityEngine;

namespace Aethiumian.AI.Nodes
{
    [Serializable]
    [Tooltip("Add a component to the game object")]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Amlos.AI.Nodes", "Aethiumian-AI")]
    public sealed class AddComponent : Call
    {
        public enum ParentMode
        {
            underSelf,
            underParent,
        }

        public TypeReference<Component> component;
        public ParentMode targetGameObject;

        public override State Execute()
        {
            switch (targetGameObject)
            {
                case ParentMode.underSelf:
                    gameObject.AddComponent(component);
                    break;
                case ParentMode.underParent:
                    if (transform.parent) transform.parent.gameObject.AddComponent(component);
                    else
                    {
                        return State.Failed;
                    }
                    break;
                default:
                    return State.Failed;
            }
            return State.Success;
        }
    }
}