using Amlos.AI.Variables;
using Minerva.Module;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Amlos.AI.Nodes
{
    [Serializable]
    public sealed class CallGameObject : Call, IMethodCaller
    {
        public bool getGameObject;
        [Constraint(VariableType.Generic, VariableType.UnityObject)]
        [DisplayIf(nameof(getGameObject), false)] public VariableReference pointingGameObject;

        public string methodName;
        public List<Parameter> parameters;
        public VariableReference result;

        public List<Parameter> Parameters { get => parameters; set => parameters = value; }
        public VariableReference Result { get => result; set => result = value; }
        public string MethodName { get => methodName; set => methodName = value; }

        public override State Execute()
        {
            object ret;

            var methods = typeof(GameObject).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
            var method = methods.Where(m => m.Name == MethodName && MethodCallers.ParameterMatches(m, parameters)).FirstOrDefault();
            GameObject gameObject = this.gameObject;
            if (getGameObject) gameObject = this.gameObject;
            else
            {
                var value = pointingGameObject.Value;
                if (value is GameObject g) gameObject = g;
                else if (value is Component c) gameObject = c.gameObject;
                else return HandleException(new ArgumentException(nameof(value)));
            }
            ret = method.Invoke(gameObject, Parameter.ToValueArray(this, method, Parameters));
            if (Result.HasReference) Result.Value = ret;


            //no return
            if (ret is null)
            {
                return State.Success;
            }
            else if (ret is bool b)
            {
                return StateOf(b);
            }
            else return State.Success;
        }

        public override void Initialize()
        {
            MethodCallers.InitializeParameters(behaviourTree, this);
        }
    }
}