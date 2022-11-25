using System;

namespace Amlos.AI
{
    [NodeTip("Determine the number of enemies")]
    [Serializable]
    public sealed class NameOfGameObject : ComparableDetermine<string>
    {
        public override string GetValue()
        {
            return gameObject.name;
        }
    }
}