namespace Aethiumian.AI.Nodes
{
    /// <summary>
    /// Decorator that succeeds only when the child's boolean result changes.
    /// </summary>
    [NodeTip("Return success when the child's result changes; otherwise failed.")]
    [System.Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Amlos.AI.Nodes", "Aethiumian-AI")]
    public sealed class ResultChanged : Decorator
    {
        private bool hasLastResult;
        private bool lastResult;

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
            hasLastResult = false;
            lastResult = false;
        }
    }
}
