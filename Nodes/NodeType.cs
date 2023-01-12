using System;

namespace Amlos.AI
{

    [Obsolete]
    public enum NodeType
    {
        none,

        decision,
        loop,
        sequence,
        condition,
        probability,
        always,
        inverter,

        call,
        determine,
        action,
    }
}