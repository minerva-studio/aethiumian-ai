using Amlos.AI.References;

namespace Amlos.AI.Nodes
{
    /// <summary>
    /// Flow node that succeeds only when the child's boolean result changes.
    /// </summary>
    [NodeTip("Return success when the child's result changes; otherwise failed.")]
    public sealed class ResultChanged : Flow
    {
        /// <summary>
        /// The child node to evaluate each tick.
        /// </summary>
        public NodeReference subtreeHead;

        private bool hasLastResult;
        private bool lastResult;

        /// <summary>
        /// Executes the child node so its result can be compared against the previous one.
        /// </summary>
        /// <returns>
        /// <see cref="State.NONE_RETURN"/> when the child is scheduled, or <see cref="State.Failed"/> when no child is assigned.
        /// </returns>
        /// <remarks>
        /// Returns <see cref="State.Failed"/> if <see cref="subtreeHead"/> has no reference.
        /// </remarks>
        public override State Execute()
        {
            if (!subtreeHead.HasReference)
            {
                return State.Failed;
            }

            return SetNextExecute(subtreeHead);
        }

        /// <summary>
        /// Compares the child's boolean result with the previous result.
        /// </summary>
        /// <param name="return">The boolean result reported by the child node.</param>
        /// <returns>
        /// <see cref="State.Success"/> when the result has changed; otherwise <see cref="State.Failed"/>.
        /// </returns>
        /// <remarks>
        /// Returns <see cref="State.Failed"/> when the first result is received or the result is unchanged.
        /// </remarks>
        public override State ReceiveReturnFromChild(bool @return)
        {
            if (!hasLastResult)
            {
                hasLastResult = true;
                lastResult = @return;
                return State.Failed;
            }

            if (lastResult == @return)
            {
                return State.Failed;
            }

            lastResult = @return;
            return State.Success;
        }

        /// <summary>
        /// Initializes node references and clears stored comparison state.
        /// </summary>
        /// <remarks>
        /// Clears cached results so the first child return will not be treated as a change.
        /// </remarks>
        public override void Initialize()
        {
            behaviourTree.GetNode(ref subtreeHead);
            hasLastResult = false;
            lastResult = false;
        }
    }
}
