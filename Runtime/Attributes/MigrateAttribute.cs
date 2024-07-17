using System;

namespace Amlos.AI
{
    /// <summary>
    /// use for migrate an old treeNode type to a new one
    /// </summary>
    [Obsolete("Due to serialization method change, Migrate Attribute can no longer migrate node class from old name to new, use MovedFrom instead")]
    public sealed class MigrateAttribute : Attribute
    {
        public Type newType;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="newType">the new type migrate to</param>
        public MigrateAttribute(Type newType)
        {
            this.newType = newType;
        }
    }
}