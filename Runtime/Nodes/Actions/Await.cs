using Amlos.AI.Variables;
using System.Threading.Tasks;

namespace Amlos.AI.Nodes
{
    [NodeTip("Awaiting a task to complete")]
    public sealed class Await : Action
    {
        public VariableReference<Task> task;
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