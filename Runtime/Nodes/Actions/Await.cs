using Aethiumian.AI.Variables;
using System.Threading.Tasks;

namespace Aethiumian.AI.Nodes
{
    [NodeTip("Awaiting a task to complete")]
    [System.Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Amlos.AI.Nodes", "Aethiumian-AI")]
    public sealed class Await : Action
    {
        [Readable]
        public VariableReference<Task> task;
        [Readable]
        public VariableReference<Task> result;

        private Task taskValue;

        public override void Awake()
        {
            taskValue = task;
        }

        public override void FixedUpdate()
        {
            if (taskValue.IsCompleted)
            {
                var value = ObjectActionBase.GetReturnedValue(taskValue);
                if (result.HasReference) result.SetValue(value);
                End(true);
            }
            else if (taskValue.IsCompleted)
            {
                End(false);
            }
        }
    }
}