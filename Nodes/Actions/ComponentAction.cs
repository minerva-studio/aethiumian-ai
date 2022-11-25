using System;
using UnityEngine;
using Component = UnityEngine.Component;

namespace Amlos.AI
{
    [Serializable]
    public sealed class ComponentAction : Action
    {
        public enum ParentMode
        {
            underSelf,
            underParent,
        }

        public ComponentReference component;
        public ParentMode targetGameObject;

        private Component instance;

        public override void ExecuteOnce()
        {
            switch (targetGameObject)
            {
                case ParentMode.underSelf:
                    instance = gameObject.AddComponent(component);
                    break;
                case ParentMode.underParent:
                    if (transform.parent) instance = transform.parent.gameObject.AddComponent(component);
                    else
                    {
                        End(false);
                        return;
                    }
                    break;
                default:
                    End(false);
                    break;
            }

            if (instance is IActionScript action)
            {
                action.Progress = new NodeProgress(this);
            }
        }


        public override void FixedUpdate()
        {
            if (!instance)
            {
                End(true);
            }
        }
    }
}