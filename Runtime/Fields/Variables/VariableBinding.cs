namespace Aethiumian.AI.Variables
{
    /// <summary>
    /// Represent a field that can bind to a variable
    /// </summary>
    public interface IVariableBinding
    {
        /// <summary>Gets the authored UUID of the variable this field points to.</summary>
        UUID UUID { get; }
        /// <summary>Gets the runtime variable currently resolved for this binding.</summary>
        RuntimeVariable RuntimeVariable { get; }

        /// <summary>
        /// Sets the authored variable reference in the editor.
        /// </summary>
        /// <param name="variable">The authored variable, or <see langword="null"/> to clear it.</param>
        void SetReference(VariableData variable);

        /// <summary>
        /// Sets the runtime resolution while constructing <see cref="BehaviourTree"/>.
        /// </summary>
        /// <param name="variable">The resolved runtime variable, or <see langword="null"/> when unresolved.</param>
        void SetRuntimeReference(RuntimeVariable variable);
    }

    public static class VariableBinding
    {
        /// <summary>Determines whether a binding has a valid runtime resolution.</summary>
        /// <param name="variableBinding">The binding to inspect.</param>
        /// <returns><see langword="true"/> when the binding has a valid runtime variable.</returns>
        public static bool IsBound(this IVariableBinding variableBinding)
        {
            return variableBinding != null
                && variableBinding.UUID != UUID.Empty
                && variableBinding.RuntimeVariable?.IsValid == true;
        }
    }
}
