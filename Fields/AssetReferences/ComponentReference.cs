using System;

namespace Amlos.AI
{
    /// <summary>
    /// class that point to reference of a Component type
    /// </summary>
    [Serializable]
    [Obsolete]
    public class ComponentReference
    {
        public string classFullName = "";
        public string assemblyFullName = "";

        internal Type GetComponentType()
        {
            return Type.GetType(assemblyFullName);
        }


        public static implicit operator Type(ComponentReference cr)
        {
            return cr.GetComponentType();
        }
    }
}
