using Aethiumian.AI.Variables;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Aethiumian.AI.Nodes
{
    [NodeMenuPath("External")]
    [NodeTip("Call a selected function once")]
    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Amlos.AI.Nodes", "Aethiumian-AI")]
    public sealed class FunctionCall : Call
    {
        public FunctionReference function = new();
        public List<Parameter> parameters = new();
        public VariableReference result = new();

        public override void Initialize()
        {
            InitializeReference(function?.targetObject);
        }

        public override State Execute()
        {
            try
            {
                MethodInfo method = FunctionRegistry.Resolve(function);
                if (method == null)
                {
                    return State.Failed;
                }

                object target = ResolveInvokeTarget(method);
                object[] values = Parameter.ToValueArray(this, method, parameters);
                object returnValue = method.Invoke(target, values);
                if (result.HasReference && method.ReturnType != typeof(void))
                {
                    result.SetValue(returnValue);
                }

                return returnValue is bool boolValue ? StateOf(boolValue) : State.Success;
            }
            catch (Exception e)
            {
                return HandleException(e);
            }
        }

        private object ResolveInvokeTarget(MethodInfo method)
        {
            if (method.IsStatic)
            {
                return null;
            }

            object target = function.targetObject?.Value;
            if (target == null)
            {
                throw new InvalidOperationException("Function receiver is not assigned.");
            }

            return VariableUtility.ImplicitConversion(method.DeclaringType, target);
        }

        private void InitializeReference(VariableReference reference)
        {
            if (reference == null || reference.IsConstant)
            {
                return;
            }

            bool hasVariable = behaviourTree.TryGetVariable(reference.UUID, out Variable variable);
            reference.SetRuntimeReference(hasVariable ? variable : null);
        }

    }
}
