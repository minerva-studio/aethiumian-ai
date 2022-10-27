using System;

namespace Amlos.AI
{
    [NodeTip("Determine the number of enemies")]
    [Serializable]
    public class NameOfGameObject : ComparableDetermine<string>
    {
        public override string GetValue()
        {
            return gameObject.name;
        }
    }
}