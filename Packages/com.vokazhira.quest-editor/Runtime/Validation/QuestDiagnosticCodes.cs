namespace Vokazhira.QuestEditor
{
    /// <summary>
    /// Коды диагностик валидатора. QE — ошибки (блокируют запуск), QW — предупреждения.
    /// </summary>
    public static class QuestDiagnosticCodes
    {
        public const string MissingStart = "QE001";
        public const string MultipleStarts = "QE002";
        public const string MissingEnd = "QE003";
        public const string EdgeMissingNode = "QE004";
        public const string EdgeMissingPort = "QE005";
        public const string IncompatiblePorts = "QE006";
        public const string PortCapacityExceeded = "QE007";
        public const string DuplicateEdge = "QE008";
        public const string EmptyQuestId = "QE009";
        public const string DuplicateQuestId = "QE010";
        public const string DuplicateNodeId = "QE011";
        public const string DuplicateObjectiveId = "QE012";
        public const string DuplicateRewardId = "QE013";
        public const string InvalidValue = "QE014";
        public const string FlowPortNotConnected = "QE015";
        public const string EndUnreachable = "QE016";
        public const string FlowCycle = "QE017";
        public const string MissingNodeType = "QE018";
        public const string RewardNotToCompletedEnd = "QE019";
        public const string NodeValidationFailed = "QE020";

        public const string NodeUnreachable = "QW001";
        public const string RewardNotConnected = "QW002";
        public const string ObjectiveWithoutDescription = "QW003";
        public const string CompletedEndWithoutRewards = "QW004";
    }
}
