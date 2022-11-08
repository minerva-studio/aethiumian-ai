using System;

namespace Amlos.AI
{

    /// <summary>
    /// node that take an action
    /// </summary>
    [Serializable]
    public abstract class Action : TreeNode
    {
        public override void Initialize() { }

        /// <summary>
        /// Call before action start execute
        /// </summary>
        public virtual void BeforeExecute()
        {
        }
        /// <summary>
        /// Called only once when action executed
        /// </summary>
        public virtual void ExecuteOnce()
        {
        }

        public sealed override void Execute()
        {
            BeforeExecute();
            behaviourTree.UpdateCall += Update;
            behaviourTree.LateUpdateCall += LateUpdate;
            behaviourTree.FixedUpdateCall += FixedUpdate;
            behaviourTree.Wait();
            ExecuteOnce();
        }

        public override void Stop()
        {
            //Debug.Log("Node " + name + "Stoped");
            behaviourTree.UpdateCall -= Update;
            behaviourTree.LateUpdateCall -= LateUpdate;
            behaviourTree.FixedUpdateCall -= FixedUpdate;
        }


        public virtual void Update() { }
        public virtual void LateUpdate() { }
        public virtual void FixedUpdate() { }
    }

    public interface IActionScript
    {
        NodeProgress Progress { get; set; }
    }
}