using Aethiumian.AI.References;
using Aethiumian.AI.Variables;
using System.Collections;

namespace Aethiumian.AI.Nodes
{
    [NodeTip("A For-Each loop")]
    [System.Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Amlos.AI.Nodes", "Aethiumian-AI")]
    public sealed class ForEach : Flow
    {
        public VariableReference enumerable;
        public VariableReference item;
        public NodeReference @event;

        private IEnumerator enumerator;

        public override State Execute()
        {
            if (!this.enumerable.HasReference)
            {
                return State.Failed;
            }

            if (this.enumerable.Value is not IEnumerable enumerable)
            {
                return State.Failed;
            }

            enumerator = enumerable.GetEnumerator();
            return TryMoveNext();
        }

        public override State ReceiveReturnFromChild(bool @return)
        {
            return TryMoveNext();
        }

        private State TryMoveNext()
        {
            // enumerator reach end
            if (!enumerator.MoveNext())
            {
                return State.Success;
            }

            if (item.HasReference) item.SetValue(enumerator.Current);
            return SetNextExecute(@event);
        }




        public override void Initialize()
        {
        }
    }
}
