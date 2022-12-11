using System;

namespace Amlos.AI
{
    /// <summary>
    /// An attribute that allow ai editor to display tooltip for the type of node
    /// </summary>
    [System.AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class NodeTipAttribute : Attribute
    {
        readonly string tip;

        /// <summary>
        /// the tip
        /// </summary>
        /// <param name="tip"></param>
        public NodeTipAttribute(string tip)
        {
            this.tip = tip;
        }

        public string Tip
        {
            get { return tip; }
        }
    }
}