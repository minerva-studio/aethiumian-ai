using System;

namespace Aethiumian.AI.Randomization
{
    public enum RandomSourceScope
    {
        Entry,
        Local,
        Static,
        Global,
    }

    [Flags]
    public enum RandomSourceScopeMask
    {
        Entry = 1 << 0,
        Local = 1 << 1,
        Static = 1 << 2,
        Global = 1 << 3,
        All = Entry | Local | Static | Global,
    }

    [Serializable]
    public struct RandomSourceBinding
    {
        public RandomSourceAsset source;
        public RandomSourceScope scope;

        public bool HasSource => source;

        public RandomSourceBinding(RandomSourceAsset source, RandomSourceScope scope)
        {
            this.source = source;
            this.scope = scope;
        }

        public static RandomSourceBinding WithScope(RandomSourceScope scope)
        {
            return new RandomSourceBinding(null, scope);
        }
    }
}
