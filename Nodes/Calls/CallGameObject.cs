using Amlos.AI.Variables;
using Minerva.Module;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Amlos.AI
{
    [Serializable]
    public sealed class CallGameObject : Call, IMethodCaller
    {
        public bool getGameObject;
        [TypeLimit(VariableType.Generic, VariableType.UnityObject)]
        [DisplayIf(nameof(getGameObject), false)] public VariableReference pointingGameObject;

        public string methodName;
        public List<Parameter> parameters;
        public VariableReference result;

        public List<Parameter> Parameters { get => parameters; set => parameters = value; }
        public VariableReference Result { get => result; set => result = value; }
        public string MethodName { get => methodName; set => methodName = value; }

        public override void Execute()
        {
            object ret;
            try
            {
                var methods = typeof(GameObject).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
                var method = methods.Where(m => m.Name == MethodName && MethodCallers.ParameterMatches(m, parameters)).FirstOrDefault();
                GameObject gameObject = this.gameObject;
                if (getGameObject) gameObject = this.gameObject;
                else
                {
                    var value = pointingGameObject.Value;
                    if (value is GameObject g) gameObject = g;
                    else if (value is Component c) gameObject = c.gameObject;
                    else throw new ArgumentException(nameof(value));
                }
                ret = method.Invoke(gameObject, Parameter.ToValueArray(this, method, Parameters));
                Log(ret);
            }
            catch (Exception e)
            {
                LogException(e);
                LogException(new ArithmeticException("Method " + MethodName + $" cannot be invoke!"));
                End(false);
                return;
            }

            if (Result.HasReference)
            {
                Result.Value = ret;
            }

            //no return
            if (ret is null)
            {
                End(true);
                return;
            }
            else if (ret is bool b)
            {
                End(b);
                return;
            }
            else End(true);
        }

        public override void Initialize()
        {
            MethodCallers.InitializeParameters(behaviourTree, this);
        }
    }
}