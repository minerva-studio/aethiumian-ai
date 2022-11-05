using System;

namespace Amlos.AI
{
    /// <summary>
    /// class that point to reference of a Component type
    /// </summary>
    [Serializable]
    public class ComponentReference
    {
        public string name;
        public string assemblyFullName;

        internal Type GetComponentType()
        {
            return Type.GetType(name);
        }


        public static implicit operator Type(ComponentReference cr)
        {
            return cr.GetComponentType();
        }
    }
}
