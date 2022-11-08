using Minerva.Module;
using UnityEngine;

/// <summary>
/// Author: Wendell
/// </summary>
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
        [Tooltip("Set AI start when enter scene")] public bool awakeStart = true;
        [Tooltip("Set AI auto restart")] public bool autoRestart = true;

#if UNITY_EDITOR
        public void OnValidate()
        {
            try
            {
                if (data != null)
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
            behaviourTree = new BehaviourTree(data, controlTarget.Exist() ?? this);
            if (awakeStart)
            {
                behaviourTree.Start();
            }
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
            if (!behaviourTree.IsRunning && autoRestart) behaviourTree.Start();
            if (behaviourTree.IsRunning) behaviourTree.FixedUpdate();
        }

        [ContextMenu("Start Behaviour Tree")]
        public void StartBehaviourTree()
        {
            if (!behaviourTree.IsRunning) behaviourTree.Start();
        }
        public void StartBehaviourTree(bool autoRestart)
        {
            this.autoRestart = autoRestart;
            if (!behaviourTree.IsRunning) behaviourTree.Start();
        }

        /// <summary>
        /// Reload the entire behaviour tree
        /// </summary>
        [ContextMenu("Reload Behaviour Tree")]
        public void Reload()
        {
            behaviourTree.End();
            behaviourTree = new BehaviourTree(data, controlTarget.Exist() ?? this);
            if (autoRestart) behaviourTree.Start();
        }

        public void Reload(bool autoRestart)
        {
            this.autoRestart = autoRestart;
            Reload();
        }



        [ContextMenu("Pause")]
        public void Pause()
        {
            behaviourTree.Pause();
        }

        [ContextMenu("Continue")]
        public void Continue()
        {
            behaviourTree.Resume();
        }
    }
}
