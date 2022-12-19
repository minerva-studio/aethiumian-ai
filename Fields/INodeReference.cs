using Minerva.Module;
using System;

namespace Amlos.AI
{
    public interface INodeReference : ICloneable
    {
        bool IsRawReference { get; }
        bool HasEditorReference { get; }
        bool HasReference { get; }
        TreeNode Node { get; set; }
        UUID UUID { get; set; }
    }
}