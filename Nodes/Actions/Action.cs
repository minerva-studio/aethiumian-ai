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
        private State exeResult;
        private bool isInFirstExecution;
        private bool isReturned;

        public override void Initialize() { }


        public sealed override State Execute()
        {
            isReturned = false;
            isInFirstExecution = true;
            exeResult = State.Wait;

            Awake(); if (IsReturnValue(exeResult)) { OnDestroy(); return exeResult; }
            behaviourTree.UpdateCall += Update;
            behaviourTree.LateUpdateCall += LateUpdate;
            behaviourTree.FixedUpdateCall += FixedUpdate;

            Start(); if (IsReturnValue(exeResult)) { OnDestroy(); return exeResult; }
            isInFirstExecution = false;
            isReturned = true;
            return exeResult;
        }

        public sealed override void Stop()
        {
            //Debug.Log("Node " + name + "Stoped");
            behaviourTree.UpdateCall -= Update;
            behaviourTree.LateUpdateCall -= LateUpdate;
            behaviourTree.FixedUpdateCall -= FixedUpdate;
            OnDestroy();
            base.Stop();
        }

        /// <summary>
        /// return node, back to its parent
        /// </summary>
        /// <returns> Whether the node has succesfully returned </returns>
        /// <param name="return"></param>
        public bool End(bool @return)
        {
            // cannot return twice
            if (isReturned) return false;

            isReturned = true;
            if (isInFirstExecution)
            {
                exeResult = StateOf(@return);
                return true;
            }

            Stop();
            return behaviourTree.ReceiveReturn(this, @return);
        }




        /**
         * Consider the following method just like unity messages
         */


        /// <summary> Call before action start execute </summary>
        public virtual void Awake() { }
        /// <summary> Called only once when action executed </summary>
        public virtual void Start() { }
        public virtual void Update() { }
        public virtual void LateUpdate() { }
        public virtual void FixedUpdate() { }
        public virtual void OnDestroy() { }
    }

    public interface IActionScript
    {
        NodeProgress Progress { get; set; }
    }
}