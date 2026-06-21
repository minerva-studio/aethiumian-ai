using System;

namespace Aethiumian.AI
{
    /// <summary>
    /// Marks a method as a preferred AI function picker candidate.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    public sealed class AIFunctionAttribute : Attribute
    {
        public string Path { get; }
        public string DisplayName { get; }

        public AIFunctionAttribute(string path = null, string displayName = null)
        {
            Path = path ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
        }
    }
}
