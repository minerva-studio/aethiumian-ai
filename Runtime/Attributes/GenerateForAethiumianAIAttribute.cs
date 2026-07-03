using System;

namespace Aethiumian.AI
{
    /// <summary>
    /// Requests Aethiumian AI analyzer and generator support for the containing assembly.
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class | AttributeTargets.Struct, Inherited = false, AllowMultiple = false)]
    public sealed class GenerateForAethiumianAIAttribute : Attribute
    {
    }
}
