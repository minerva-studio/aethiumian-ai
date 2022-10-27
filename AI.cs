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
        public bool awakeStart = true;
        private bool autoStart;

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
            behaviourTree = new BehaviourTree(data, controlTarget);
            autoStart = awakeStart;
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
            if (!behaviourTree.IsRunning && autoStart) behaviourTree.Start();
            if (behaviourTree.IsRunning) behaviourTree.FixedUpdate();
        }

        [ContextMenu("Start Behaviour Tree")]
        public void StartBehaviourTree(bool autoStart = true)
        {
            this.autoStart = autoStart;
            if (!behaviourTree.IsRunning) behaviourTree.Start();
        }

        [ContextMenu("Reload Behaviour Tree")]
        public void Reload()
        {
            behaviourTree.End();
            behaviourTree = new BehaviourTree(data, controlTarget);
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
