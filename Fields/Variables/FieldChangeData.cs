using System;

namespace Amlos.AI
{
    [Serializable]
    public class FieldChangeData
    {
        public string name;
        public VariableField data;
    }

    [Serializable]
    public class FieldPointer
    {
        public string name;
        public VariableReference data;
    }
}
