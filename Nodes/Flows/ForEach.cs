using System;
using System.Collections;
using System.Collections.Generic;

namespace Amlos.AI
{
    [NodeTip("A For-Each loop")]
    public sealed class ForEach : Flow
    {
        public VariableReference enumerable;
        public VariableReference item;
        public NodeReference @event;

        private IEnumerator enumerator;

        public override void Execute()
        {
            if (!this.enumerable.HasRuntimeReference)
            {
                End(false);
                return;
            }

            if (this.enumerable.Value is not IEnumerable enumerable)
            {
                End(false);
                return;
            }

            enumerator = enumerable.GetEnumerator();
            TryMoveNext();
        }

        public override void ReceiveReturnFromChild(bool @return)
        {
            TryMoveNext();
        }

        private void TryMoveNext()
        {
            if (!enumerator.MoveNext())
            {
                End(true);
                return;
            }

            if (item.HasRuntimeReference) item.Value = enumerator.Current;
            SetNextExecute(@event);
        }




        public override void Initialize()
        {
            @event = behaviourTree.References[@event];
        }
    }
}