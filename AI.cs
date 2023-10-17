using UnityEngine;

namespace Amlos.AI
{
    /// <summary>
    /// Driver of Behaviour tree
    /// </summary>
    /// <remarks>
    /// Author: Wendell
    /// </remarks>
    public class AI : MonoBehaviour
    {
        public MonoBehaviour controlTarget;
        public BehaviourTreeData data;
        public BehaviourTree behaviourTree;
        [Tooltip("Set AI start when enter scene")] public bool awakeStart = true;
        [Tooltip("Set AI auto restart")] public bool autoRestart = true;
        private bool allowAutoRestart = false;


#if UNITY_EDITOR
        public void OnValidate()
        {
            try
            {
                if (data == null) return;
                if (data.targetScript != null)
                {
                    controlTarget = GetComponent(data.targetScript.GetClass()) as MonoBehaviour;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                Debug.LogError("Cannot found target script " + data.targetScript.GetClass() + " on the same GameObject!");
                throw;
            }
        }
#endif


        void Start()
        {
            if (!data)
            {
                Debug.LogWarning($"No behaviour tree data has been assigned to AI Component on {name}", this);
                enabled = false;
                return;
            }

            CreateBehaviourTree();
            allowAutoRestart = autoRestart;
            if (awakeStart)
            {
                behaviourTree.Start();
            }
            else { allowAutoRestart = false; }
        }


        void Update()
        {
            if (behaviourTree == null) return;
            if (behaviourTree.IsRunning) behaviourTree.Update();
        }

        void LateUpdate()
        {
            if (behaviourTree == null) return;
            if (behaviourTree.IsRunning) behaviourTree.LateUpdate();
        }

        void FixedUpdate()
        {
            if (behaviourTree == null) return;
            if (!behaviourTree.IsRunning && allowAutoRestart) behaviourTree.Start();
            if (behaviourTree.IsRunning) behaviourTree.FixedUpdate();
        }

        private void OnDestroy()
        {
            if (behaviourTree.IsRunning) behaviourTree.End();
        }

        public void CreateBehaviourTree()
        {
            behaviourTree = new BehaviourTree(data, gameObject, controlTarget);
        }

        [ContextMenu("Start Behaviour Tree")]
        public void StartBehaviourTree() => StartBehaviourTree(autoRestart);
        public void StartBehaviourTree(bool autoRestart)
        {
            if (behaviourTree == null) return;
            this.allowAutoRestart = autoRestart;
            if (!behaviourTree.IsRunning) behaviourTree.Start();
        }

        /// <summary>
        /// Reload the entire behaviour tree
        /// </summary>
        [ContextMenu("Reload Behaviour Tree")]
        public void Reload()
        {
            if (behaviourTree == null) return;
            if (behaviourTree.IsRunning) behaviourTree.End();
            CreateBehaviourTree();
            if (autoRestart) behaviourTree.Start();
        }

        public void Reload(bool autoRestart)
        {
            this.allowAutoRestart = autoRestart;
            Reload();
        }



        [ContextMenu("Pause")]
        public void Pause()
        {
            if (behaviourTree == null) return;
            behaviourTree.Pause();
        }

        [ContextMenu("Continue")]
        public void Continue()
        {
            if (behaviourTree == null) return;
            behaviourTree.Resume();
        }
    }
}
