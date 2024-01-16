using Minerva.Module;
using UnityEngine;

namespace Amlos.AI
{
    /// <summary>
    /// Driver of Behaviour tree 
    /// </summary>
    public class AI : MonoBehaviour
    {
        public MonoBehaviour controlTarget;
        public BehaviourTreeData data;
        public BehaviourTree behaviourTree;
        [Tooltip("Set AI start when enter scene")]
        public bool awakeStart = true;
        [Tooltip("Set AI auto restart"), HideInRuntime]
        public bool autoRestart = true;

        /// <summary>
        /// This is the final state of whether AI will auto restarts
        /// </summary>
        private bool _autoRestart = false;


        public bool IsRunning => behaviourTree?.IsRunning == true;


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
        private void Awake()
        {
            if (!data)
            {
                Debug.LogWarning($"No behaviour tree data has been assigned to AI Component on {name}", this);
                enabled = false;
                return;
            }

            CreateBehaviourTree();
            _autoRestart = autoRestart;
        }

        void Start()
        {
            if (awakeStart && !behaviourTree.IsRunning) { behaviourTree.Start(); }
            else { _autoRestart = false; }
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
            if (!behaviourTree.IsRunning && _autoRestart) behaviourTree.Start();
            if (behaviourTree.IsRunning) behaviourTree.FixedUpdate();
        }

        private void OnDestroy()
        {
            if (behaviourTree.IsRunning) behaviourTree.End();
        }

        void CreateBehaviourTree()
        {
            behaviourTree = new BehaviourTree(data, gameObject, controlTarget);
        }

        [ContextMenu("Start Behaviour Tree")]
        public void StartBehaviourTree() => Start(autoRestart);
#pragma warning disable UNT0006 
        public void Start(bool autoRestart)
#pragma warning restore UNT0006  
        {
            if (behaviourTree == null) return;
            this._autoRestart = autoRestart;
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
            this._autoRestart = autoRestart;
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

        [ContextMenu("End")]
        public void End()
        {
            if (behaviourTree == null) return;
            behaviourTree.End();
        }
        public void End(bool autoRestart)
        {
            if (behaviourTree == null) return;
            behaviourTree.End();
            this._autoRestart = autoRestart;
        }
    }
}
