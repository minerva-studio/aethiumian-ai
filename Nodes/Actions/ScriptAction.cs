using Amlos.AI.Variables;
using System;
using UnityEngine;

namespace Amlos.AI.Nodes
{
    /// <summary>
    /// Call the method every frame
    /// </summary>
    /// <remarks>
    /// Use <see cref="ComponentAction"/> instead
    /// </remarks>
    [DoNotRelease]
    [Serializable]
    [Obsolete]
    public sealed class ScriptAction : ObjectActionBase, IMethodCaller
    {
        public override void Call()
        {
            var method = behaviourTree.Script.GetType().GetMethod(methodName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

            object ret = method.Invoke(behaviourTree.Script, Parameter.ToValueArray(this, method, parameters));
            if (Result.HasReference) Result.Value = ret;

            ActionEnd();
        }
    }
}