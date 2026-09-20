using System;

namespace Aethiumian.AI.Navigation
{
    /// <summary>Actions a planner can generate, not node state or the action used to reach a node.</summary>
    [Flags]
    public enum NavigationActions
    {
        GroundMove = 1,
        Jump = 2,
        Fall = 4,
        DropThrough = 8,
        Fly = 16,
    }

}
