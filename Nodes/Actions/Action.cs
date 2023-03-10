using Amlos.AI.References;
using System;

namespace Amlos.AI.Nodes
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

        public sealed override State Execute()
        {
            BeforeExecute();
            behaviourTree.UpdateCall += Update;
            behaviourTree.LateUpdateCall += LateUpdate;
            behaviourTree.FixedUpdateCall += FixedUpdate;
            ExecuteOnce();
            return State.Wait;
        }

        public override void Stop()
        {
            //Debug.Log("Node " + name + "Stoped");
            behaviourTree.UpdateCall -= Update;
            behaviourTree.LateUpdateCall -= LateUpdate;
            behaviourTree.FixedUpdateCall -= FixedUpdate;
            base.Stop();
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