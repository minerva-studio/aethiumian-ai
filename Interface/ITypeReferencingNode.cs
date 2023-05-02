using Amlos.AI.References;

namespace Amlos.AI
{
    /// <summary>
    /// Node that has type reference field
    /// </summary>
    public interface ITypeReferencingNode
    {
        TypeReference TypeReference { get; }
    }
}
