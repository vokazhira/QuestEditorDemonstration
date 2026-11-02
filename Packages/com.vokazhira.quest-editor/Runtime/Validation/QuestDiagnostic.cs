namespace Vokazhira.QuestEditor
{
    /// <summary>
    /// Результат одной проверки валидатора. <see cref="NodeId"/> — узел, к которому относится проблема, или null.
    /// </summary>
    public readonly struct QuestDiagnostic
    {
        public QuestDiagnostic(QuestDiagnosticSeverity severity, string code, string message, string nodeId = null)
        {
            Severity = severity;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
            NodeId = string.IsNullOrEmpty(nodeId) ? null : nodeId;
        }

        public QuestDiagnosticSeverity Severity { get; }
        public string Code { get; }
        public string Message { get; }
        public string NodeId { get; }

        public override string ToString()
        {
            return NodeId == null
                ? $"[{Severity}] {Code}: {Message}"
                : $"[{Severity}] {Code}: {Message} (node {NodeId})";
        }
    }
}
