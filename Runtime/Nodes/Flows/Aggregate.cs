using Aethiumian.AI.Attributes;
using Aethiumian.AI.References;
using System;

namespace Aethiumian.AI.Nodes
{
    /// <summary>Executes every child in order and aggregates their results into one value.</summary>
    [Serializable]
    [NodeTip("Executes every child in order, then returns All, Any, True, or False.")]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Amlos.AI.Nodes", "Aethiumian-AI")]
    public sealed class Aggregate : Flow
    {
        public enum ResultMode
        {
            All,
            Any,
            True,
            False,
        }

        public NodeReference[] events = Array.Empty<NodeReference>();
        public ResultMode resultMode = ResultMode.All;

        [ReadOnly] private NodeReference current;
        [ReadOnly] private int index;
        [ReadOnly] private bool aggregate;

        /// <inheritdoc />
        public override State ReceiveReturnFromChild(bool @return)
        {
            aggregate = resultMode switch
            {
                ResultMode.All => aggregate && @return,
                ResultMode.Any => aggregate || @return,
                ResultMode.True => true,
                ResultMode.False => false,
                _ => aggregate,
            };
            if (index == events.Length - 1)
            {
                return StateOf(aggregate);
            }

            index++;
            current = events[index];
            return SetNextExecute(current);
        }

        /// <inheritdoc />
        public override State Execute()
        {
            aggregate = GetInitialResult();
            if (events.Length == 0)
            {
                return StateOf(aggregate);
            }

            index = 0;
            current = events[0];
            return SetNextExecute(current);
        }

        /// <inheritdoc />
        public override void Initialize()
        {
            index = -1;
            current = null;
            aggregate = GetInitialResult();
        }

        /// <summary>Gets the empty-set identity or fixed result for the configured mode.</summary>
        private bool GetInitialResult()
        {
            return resultMode is ResultMode.All or ResultMode.True;
        }
    }
}
