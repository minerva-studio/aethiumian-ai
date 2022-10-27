using Minerva.Module;
using System.Collections.Generic;

namespace Amlos.AI
{
    /// <summary>
    /// a placeholder node used for represent invalid generic node when they are trying to convert to a normal node
    /// </summary>
    public class PlaceholderNode : TreeNode
    {
        public string originalType;
        public string values; 


        /// <summary>
        /// No implementation because it is a placeholder only
        /// </summary>
        /// <exception cref="System.NotImplementedException">always</exception>
        public override void Execute()
        {
            throw new System.NotImplementedException();
        }

        /// <summary>
        /// No implementation because it is a placeholder only
        /// </summary>
        /// <exception cref="System.NotImplementedException">always</exception>
        public override void Initialize()
        {
            throw new System.NotImplementedException();
        }
    }

}