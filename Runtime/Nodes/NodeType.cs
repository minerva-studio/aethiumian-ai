using System;

namespace Aethiumian.AI.Nodes
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