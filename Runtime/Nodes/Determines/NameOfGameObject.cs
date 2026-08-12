using System;

namespace Aethiumian.AI.Nodes
{
    [NodeTip("Reads the name of a referenced GameObject for comparison.")]
    [Serializable]
    public sealed class NameOfGameObject : ComparableDetermine<string>
    {
        public override string GetValue()
        {
            return gameObject.name;
        }
    }
}
