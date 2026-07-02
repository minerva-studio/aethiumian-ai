using System;
using UnityEditor;

namespace Aethiumian.AI.Editor
{
    /// <summary>
    /// Restores the editor GUI indent level when disposed.
    /// </summary>
    internal sealed class IndentScope : IDisposable
    {
        private readonly int previousIndent;
        private bool disposed;

        public static IndentScope Increase => new();

        public IndentScope(int indentation = 1)
        {
            previousIndent = EditorGUI.indentLevel;
            EditorGUI.indentLevel += indentation;
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            EditorGUI.indentLevel = previousIndent;
        }
    }
}
