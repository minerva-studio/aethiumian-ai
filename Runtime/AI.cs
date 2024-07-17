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






        public void SetVariable(string name, object value)
        {
            behaviourTree?.SetVariable(name, value);
        }

        public void SetGlobalVariable(string name, object value)
        {
            BehaviourTree.SetGlobalVariable(name, value);
        }

        public void SetVariable(string name, object value, bool isGlobal = false)
        {
            if (isGlobal)
            {
                SetGlobalVariable(name, value);
            }
            else
            {
                behaviourTree.SetVariable(name, value);
            }
        }

        public void SetBool(string name, bool value)
        {
            behaviourTree.SetVariable(name, value);
        }

        public void SetFloat(string name, float value)
        {
            behaviourTree.SetVariable(name, value);
        }

        public void SetInt(string name, int value)
        {
            behaviourTree.SetVariable(name, value);
        }

        public void SetVector2(string name, Vector2 value)
        {
            behaviourTree.SetVariable(name, value);
        }

        public void SetVector3(string name, Vector3 value)
        {
            behaviourTree.SetVariable(name, value);
        }

        public void SetVector4(string name, Vector4 value)
        {
            behaviourTree.SetVariable(name, value);
        }

        public void SetColor(string name, Color value)
        {
            behaviourTree.SetVariable(name, value);
        }

        public void SetObject(string name, UnityEngine.Object value)
        {
            behaviourTree.SetVariable(name, value);
        }

        public void SetGlobalBool(string name, bool value)
        {
            BehaviourTree.SetGlobalVariable(name, value);
        }

        public void SetGlobalFloat(string name, float value)
        {
            BehaviourTree.SetGlobalVariable(name, value);
        }

        public void SetGlobalInt(string name, int value)
        {
            BehaviourTree.SetGlobalVariable(name, value);
        }

        public void SetGlobalVector2(string name, Vector2 value)
        {
            BehaviourTree.SetGlobalVariable(name, value);
        }

        public void SetGlobalVector3(string name, Vector3 value)
        {
            BehaviourTree.SetGlobalVariable(name, value);
        }

        public void SetGlobalVector4(string name, Vector4 value)
        {
            BehaviourTree.SetGlobalVariable(name, value);
        }

        public void SetGlobalColor(string name, Color value)
        {
            BehaviourTree.SetGlobalVariable(name, value);
        }

        public void SetGlobalObject(string name, UnityEngine.Object value)
        {
            BehaviourTree.SetGlobalVariable(name, value);
        }

        public void AnimationEvent_SetVariable(AnimationEvent animationEvent)
        {
            string raw = animationEvent.stringParameter;
            var arr = raw.Split("=");
            string name = arr[0].StartsWith('#') ? arr[0][1..] : arr[0];
            string valueString = arr.Length <= 1 ? null : arr[1];

            object value = null;

            var type = behaviourTree.Variables.GetVariableType(name);
            if (!type.HasValue)
            {
                return;
            }
            var typeValue = type.Value;
            switch (typeValue)
            {
                case VariableType.String:
                    if (string.IsNullOrEmpty(valueString))
                    {
                        value = string.Empty;
                    }
                    break;
                case VariableType.Int:
                    value = animationEvent.intParameter;
                    break;
                case VariableType.Float:
                    value = animationEvent.floatParameter;
                    break;
                case VariableType.Bool:
                    value = animationEvent.intParameter != 0;
                    break;
                case VariableType.Vector2:
                    if (string.IsNullOrEmpty(valueString))
                        return;
                    if (!VectorUtility.TryParseVector2(valueString, out var val))
                        return;
                    else value = val;
                    break;
                case VariableType.Vector3:
                    if (string.IsNullOrEmpty(valueString))
                        return;
                    if (!VectorUtility.TryParseVector3(valueString, out var val3))
                        return;
                    else value = val3;
                    break;
                case VariableType.Vector4:
                    if (string.IsNullOrEmpty(valueString))
                        return;
                    if (!VectorUtility.TryParseVector4(valueString, out var val4))
                        return;
                    else value = val4;
                    break;
                case VariableType.UnityObject:
                    value = animationEvent.objectReferenceParameter;
                    break;
                case VariableType.Generic:
                    return;
                default:
                    break;
            }

            SetVariable(name, value, arr[0].StartsWith('#'));
        }
    }
}
