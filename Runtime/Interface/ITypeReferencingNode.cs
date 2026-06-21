using Aethiumian.AI.References;

namespace Aethiumian.AI
{
    /// <summary>
    /// Node that has type reference field
    /// </summary>
    public interface ITypeReferencingNode
    {
        TypeReference TypeReference { get; }
    }
}
