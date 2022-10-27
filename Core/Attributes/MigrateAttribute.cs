using System;

namespace Amlos.AI
{
    /// <summary>
    /// use for migrate an old treeNode type to a new one
    /// </summary>
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