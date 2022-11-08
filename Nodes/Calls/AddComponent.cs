using System;

namespace Amlos.AI
{
    [Serializable]
    public class AddComponent : Call
    {
        public enum ParentMode
        {
            underSelf,
            underParent,
        }

        public ComponentReference component;
        public ParentMode targetGameObject;

        public override void Execute()
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
                        End(false);
                        return;
                    }
                    break;
                default:
                    End(false);
                    break;
            }
            End(true);
        }
    }
}