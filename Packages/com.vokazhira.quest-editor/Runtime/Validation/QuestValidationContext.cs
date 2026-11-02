using System.Collections.Generic;

namespace Vokazhira.QuestEditor
{
    /// <summary>
    /// Передаётся в <see cref="QuestNodeData.Validate"/>. Диагностики, добавленные узлом,
    /// автоматически привязываются к этому узлу.
    /// </summary>
    public sealed class QuestValidationContext
    {
        private readonly List<QuestDiagnostic> diagnostics;

        internal QuestValidationContext(QuestGraphAsset graph, List<QuestDiagnostic> diagnostics)
        {
            Graph = graph;
            this.diagnostics = diagnostics;
        }

        public QuestGraphAsset Graph { get; }

        public QuestNodeData Node { get; internal set; }

        public void AddError(string code, string message)
        {
            Add(QuestDiagnosticSeverity.Error, code, message);
        }

        public void AddWarning(string code, string message)
        {
            Add(QuestDiagnosticSeverity.Warning, code, message);
        }

        public void AddInfo(string code, string message)
        {
            Add(QuestDiagnosticSeverity.Info, code, message);
        }

        public void Add(QuestDiagnosticSeverity severity, string code, string message)
        {
            diagnostics.Add(new QuestDiagnostic(severity, code, message, Node?.Id));
        }
    }
}
