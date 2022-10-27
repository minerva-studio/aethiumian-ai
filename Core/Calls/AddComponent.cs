using System;

namespace Amlos.AI
{
    [DoNotRelease]
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
                    transform.parent.gameObject.AddComponent(component);
                    break;
                default:
                    break;
            }

            End(true);
        } 
    }
}