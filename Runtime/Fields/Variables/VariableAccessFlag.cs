using System;

namespace Amlos.AI.Variables
{
    [Flags]
    public enum VariableAccessFlag
    {
        None = 0,
        Read = 1,
        Write = 2,
        All = Read | Write
    }
}
