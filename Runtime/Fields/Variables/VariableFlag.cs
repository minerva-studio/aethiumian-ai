using System;

namespace Amlos.AI.Variables
{
    [Flags]
    public enum VariableFlag
    {
        /// <summary>
        /// A static variable, global to all btree of same type
        /// </summary>
        Static = 1,
        /// <summary>
        /// Global to all btree
        /// </summary>
        Global = 2,
        /// <summary>
        /// Standard vars, transform/gameobject/targetscript
        /// </summary>
        Standard = 4,
        /// <summary>
        /// Is targeting the target script
        /// </summary>
        FromScript = 8,
        /// <summary>
        /// Is read from <see cref="AIVariableAttribute"/>
        /// </summary>
        FromAttribute = 16,
        /// <summary>
        /// like from script: int A => 1
        /// </summary>
        FromScriptAttribute = FromScript | FromAttribute,
        /// <summary>
        /// like from script: static int A => 1
        /// </summary>
        FromScriptAttributeStaticVariable = FromScript | FromAttribute | Static,
    }
}