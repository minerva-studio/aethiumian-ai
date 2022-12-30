using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Amlos.AI
{
    public abstract class ObjectCallBase : Call, IMethodCaller
    {
        public string methodName;
        public List<Parameter> parameters;
        public VariableReference result;


        public string MethodName { get => methodName; set => methodName = value; }
        public List<Parameter> Parameters { get => parameters; set => parameters = value; }
        public VariableReference Result => result;
         
        public override void Initialize()
        {
            MethodCallers.InitializeParameters(behaviourTree, this);
        }

        protected void Call(object obj, Type referType)
        {
            object ret;
            try
            {
                var methods = referType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
                var method = methods.Where(m => m.Name == MethodName && MethodCallers.ParameterMatches(m, parameters)).FirstOrDefault();

                ret = method.Invoke(obj, Parameter.ToValueArray(this, Parameters));
                Debug.Log(ret);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Debug.LogException(new ArithmeticException("Method " + MethodName + $" in class {referType?.Name ?? "(null)"} cannot be invoke!"));
                End(false);
                return;
            }

            if (Result.HasRuntimeReference)
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
    }
}