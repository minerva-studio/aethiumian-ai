using System;
using System.Collections.Generic;

namespace Aethiumian.AI.Editor.Exporting
{
    /// <summary>Severity assigned to an issue found while projecting a behaviour tree.</summary>
    public enum BehaviourTreeDomDiagnosticSeverity
    {
        Info,
        Warning,
        Error,
    }

    /// <summary>Structured diagnostic emitted by the read-only DOM exporter.</summary>
    public sealed class BehaviourTreeDomDiagnostic
    {
        internal BehaviourTreeDomDiagnostic(
            string code,
            BehaviourTreeDomDiagnosticSeverity severity,
            UUID nodeId,
            string fieldPath,
            string message)
        {
            Code = code;
            Severity = severity;
            NodeId = nodeId;
            FieldPath = fieldPath ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public string Code { get; }
        public BehaviourTreeDomDiagnosticSeverity Severity { get; }
        public UUID NodeId { get; }
        public string FieldPath { get; }
        public string Message { get; }
    }

    /// <summary>Result of exporting a read-only semantic behaviour-tree DOM.</summary>
    public sealed class BehaviourTreeDomExportResult
    {
        internal BehaviourTreeDomExportResult(
            string content,
            IReadOnlyList<BehaviourTreeDomDiagnostic> diagnostics,
            int exportedNodeCount)
        {
            Content = content ?? string.Empty;
            Diagnostics = diagnostics ?? Array.Empty<BehaviourTreeDomDiagnostic>();
            ExportedNodeCount = exportedNodeCount;
        }

        public string Content { get; }
        public IReadOnlyList<BehaviourTreeDomDiagnostic> Diagnostics { get; }
        public int ExportedNodeCount { get; }

        public bool HasErrors
        {
            get
            {
                foreach (BehaviourTreeDomDiagnostic diagnostic in Diagnostics)
                {
                    if (diagnostic.Severity == BehaviourTreeDomDiagnosticSeverity.Error)
                    {
                        return true;
                    }
                }

                return false;
            }
        }
    }
}
