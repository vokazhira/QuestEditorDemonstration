using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Vokazhira.QuestEditor.Tests
{
    public sealed class QuestGraphValidatorTests
    {
        private readonly List<Object> createdObjects = new List<Object>();
        private QuestTestGraphs.ForestQuest quest;

        [SetUp]
        public void SetUp()
        {
            quest = QuestTestGraphs.CreateForestQuest();
            createdObjects.Add(quest.Graph);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var created in createdObjects)
            {
                Object.DestroyImmediate(created);
            }

            createdObjects.Clear();
        }

        [Test]
        public void ValidGraph_HasNoDiagnostics()
        {
            var diagnostics = Validate();

            Assert.That(diagnostics, Is.Empty, string.Join("\n", diagnostics));
        }

        [Test]
        public void Validate_NullGraph_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => QuestGraphValidator.Validate(null));
        }

        [Test]
        public void Rule01_NoStart_IsError()
        {
            quest.Graph.RemoveNode(quest.Start.Id);

            AssertHasError(Validate(), QuestDiagnosticCodes.MissingStart);
        }

        [Test]
        public void Rule01_SeveralStarts_IsErrorOnEachStart()
        {
            var secondStart = new StartNodeData();
            quest.Graph.AddNode(secondStart);

            var diagnostics = Validate();

            AssertHasError(diagnostics, QuestDiagnosticCodes.MultipleStarts, quest.Start.Id);
            AssertHasError(diagnostics, QuestDiagnosticCodes.MultipleStarts, secondStart.Id);
        }

        [Test]
        public void Rule02_NoEnd_IsError()
        {
            quest.Graph.RemoveNode(quest.Completed.Id);
            quest.Graph.RemoveNode(quest.Failed.Id);

            AssertHasError(Validate(), QuestDiagnosticCodes.MissingEnd);
        }

        [Test]
        public void Rule03_EdgeToMissingNode_IsError()
        {
            QuestTestGraphs.AddRawEdge(quest.Graph, quest.Failed.Id, QuestPortIds.Out, "missing-node", QuestPortIds.In);

            AssertHasError(Validate(), QuestDiagnosticCodes.EdgeMissingNode, quest.Failed.Id);
        }

        [Test]
        public void Rule03_EdgeToMissingPort_IsError()
        {
            QuestTestGraphs.AddRawEdge(quest.Graph, quest.Kill.Id, "no-such-port", quest.Failed.Id, QuestPortIds.In);

            AssertHasError(Validate(), QuestDiagnosticCodes.EdgeMissingPort, quest.Kill.Id);
        }

        [Test]
        public void Rule03_RewardEdgeToFailedEnd_IsMissingPortError()
        {
            quest.Completed.Result = QuestEndResult.Failed;

            AssertHasError(Validate(), QuestDiagnosticCodes.EdgeMissingPort, quest.Completed.Id);
        }

        [Test]
        public void Rule04_IncompatiblePorts_IsError()
        {
            QuestTestGraphs.AddRawEdge(quest.Graph, quest.Kill.Id, QuestPortIds.Out, quest.Completed.Id, QuestPortIds.Rewards);

            AssertHasError(Validate(), QuestDiagnosticCodes.IncompatiblePorts, quest.Kill.Id);
        }

        [Test]
        public void Rule04_SingleOutputWithTwoEdges_IsCapacityError()
        {
            QuestTestGraphs.AddRawEdge(quest.Graph, quest.Start.Id, QuestPortIds.Out, quest.Failed.Id, QuestPortIds.In);

            AssertHasError(Validate(), QuestDiagnosticCodes.PortCapacityExceeded, quest.Start.Id);
        }

        [Test]
        public void Rule04_DuplicateEdge_IsError()
        {
            QuestTestGraphs.AddRawEdge(quest.Graph, quest.Gold.Id, QuestPortIds.Reward, quest.Completed.Id, QuestPortIds.Rewards);

            var diagnostics = Validate();

            AssertHasError(diagnostics, QuestDiagnosticCodes.DuplicateEdge, quest.Gold.Id);
            AssertHasError(diagnostics, QuestDiagnosticCodes.PortCapacityExceeded, quest.Gold.Id);
        }

        [Test]
        public void Rule05_EmptyQuestId_IsError()
        {
            quest.Graph.QuestId = " ";

            AssertHasError(Validate(), QuestDiagnosticCodes.EmptyQuestId);
        }

        [Test]
        public void Rule05_QuestIdUsedByAnotherQuest_IsError()
        {
            var other = QuestTestGraphs.CreateForestQuest(quest.Graph.QuestId);
            createdObjects.Add(other.Graph);

            var diagnostics = QuestGraphValidator.Validate(quest.Graph, new[] { other.Graph });

            AssertHasError(diagnostics, QuestDiagnosticCodes.DuplicateQuestId);
        }

        [Test]
        public void Rule05_GraphItselfInOtherQuestsList_IsIgnored()
        {
            var diagnostics = QuestGraphValidator.Validate(quest.Graph, new[] { quest.Graph });

            AssertHasNo(diagnostics, QuestDiagnosticCodes.DuplicateQuestId);
        }

        [Test]
        public void Rule06_DuplicateNodeId_IsError()
        {
            var clone = new CollectObjective { ItemId = "herb", Description = "x" };
            QuestTestGraphs.SetNodeId(clone, quest.Kill.Id);
            QuestTestGraphs.AddRawNode(quest.Graph, clone);

            AssertHasError(Validate(), QuestDiagnosticCodes.DuplicateNodeId, quest.Kill.Id);
        }

        [Test]
        public void Rule06_DuplicateObjectiveId_IsError()
        {
            var second = new CollectObjective { ItemId = "herb", Description = "x", ObjectiveId = quest.Kill.ObjectiveId };
            quest.Graph.AddNode(second);

            AssertHasError(Validate(), QuestDiagnosticCodes.DuplicateObjectiveId, second.Id);
        }

        [Test]
        public void Rule06_DuplicateRewardId_IsError()
        {
            quest.Experience.RewardId = quest.Gold.RewardId;

            AssertHasError(Validate(), QuestDiagnosticCodes.DuplicateRewardId, quest.Experience.Id);
        }

        [Test]
        public void Rule07_NonPositiveRequiredCount_IsError()
        {
            quest.Kill.SetRequiredCount(0);

            AssertHasError(Validate(), QuestDiagnosticCodes.InvalidValue, quest.Kill.Id);
        }

        [Test]
        public void Rule07_NonPositiveAmount_IsError()
        {
            quest.Gold.Amount = 0;
            quest.Experience.Amount = -5;

            var diagnostics = Validate();

            AssertHasError(diagnostics, QuestDiagnosticCodes.InvalidValue, quest.Gold.Id);
            AssertHasError(diagnostics, QuestDiagnosticCodes.InvalidValue, quest.Experience.Id);
        }

        [Test]
        public void Rule07_EmptyEnemyId_IsError()
        {
            quest.Kill.EnemyId = string.Empty;

            AssertHasError(Validate(), QuestDiagnosticCodes.InvalidValue, quest.Kill.Id);
        }

        [Test]
        public void Rule07_EmptyItemId_IsError()
        {
            var collect = new CollectObjective { Description = "x" };
            quest.Graph.AddNode(collect);

            AssertHasError(Validate(), QuestDiagnosticCodes.InvalidValue, collect.Id);
        }

        [Test]
        public void Rule07_EmptyConditionQuestId_IsError()
        {
            quest.Condition.QuestId = string.Empty;

            AssertHasError(Validate(), QuestDiagnosticCodes.InvalidValue, quest.Condition.Id);
        }

        [Test]
        public void Rule08_ObjectiveWithoutOutput_IsError()
        {
            quest.Graph.Disconnect(quest.Kill.Id, QuestPortIds.Out, quest.Condition.Id, QuestPortIds.In);

            var diagnostics = Validate();

            AssertHasError(diagnostics, QuestDiagnosticCodes.FlowPortNotConnected, quest.Kill.Id);
            AssertHasError(diagnostics, QuestDiagnosticCodes.FlowPortNotConnected, quest.Condition.Id);
        }

        [Test]
        public void Rule08_ConditionBranchNotConnected_IsError()
        {
            quest.Graph.Disconnect(quest.Condition.Id, QuestPortIds.False, quest.Failed.Id, QuestPortIds.In);

            var diagnostics = Validate();

            AssertHasError(diagnostics, QuestDiagnosticCodes.FlowPortNotConnected, quest.Condition.Id);
            AssertHasError(diagnostics, QuestDiagnosticCodes.FlowPortNotConnected, quest.Failed.Id);
        }

        [Test]
        public void Rule09_NoEndReachableFromStart_IsError()
        {
            quest.Graph.Disconnect(quest.Start.Id, QuestPortIds.Out, quest.Kill.Id, QuestPortIds.In);

            AssertHasError(Validate(), QuestDiagnosticCodes.EndUnreachable, quest.Start.Id);
        }

        [Test]
        public void Rule09_FlowCycle_IsError()
        {
            var collect = new CollectObjective { ItemId = "herb", Description = "x" };
            quest.Graph.AddNode(collect);
            quest.Graph.Disconnect(quest.Condition.Id, QuestPortIds.False, quest.Failed.Id, QuestPortIds.In);
            QuestTestGraphs.Connect(quest.Graph, quest.Condition, QuestPortIds.False, collect, QuestPortIds.In);
            QuestTestGraphs.Connect(quest.Graph, collect, QuestPortIds.Out, quest.Kill, QuestPortIds.In);

            AssertHasError(Validate(), QuestDiagnosticCodes.FlowCycle);
        }

        [Test]
        public void Rule10_MissingNodeType_IsError()
        {
            QuestTestGraphs.AddRawNode(quest.Graph, null);

            AssertHasError(Validate(), QuestDiagnosticCodes.MissingNodeType);
        }

        [Test]
        public void Rule11_RewardConnectedToNonEndNode_IsError()
        {
            var sink = new RewardSinkTestNode();
            var bonus = new GoldReward { Amount = 5 };
            quest.Graph.AddNode(sink);
            quest.Graph.AddNode(bonus);
            QuestTestGraphs.Connect(quest.Graph, bonus, QuestPortIds.Reward, sink, QuestPortIds.Rewards);

            AssertHasError(Validate(), QuestDiagnosticCodes.RewardNotToCompletedEnd, bonus.Id);
        }

        [Test]
        public void NodeValidateThrowing_IsReportedAsError()
        {
            var throwing = new ThrowingValidationTestNode();
            quest.Graph.AddNode(throwing);

            AssertHasError(Validate(), QuestDiagnosticCodes.NodeValidationFailed, throwing.Id);
        }

        [Test]
        public void Warning_NodeNotReachableFromStart()
        {
            var collect = new CollectObjective { ItemId = "herb", Description = "x" };
            quest.Graph.AddNode(collect);

            AssertHasWarning(Validate(), QuestDiagnosticCodes.NodeUnreachable, collect.Id);
        }

        [Test]
        public void Warning_RewardNotConnected()
        {
            var bonus = new GoldReward { Amount = 5 };
            quest.Graph.AddNode(bonus);

            var diagnostics = Validate();

            AssertHasWarning(diagnostics, QuestDiagnosticCodes.RewardNotConnected, bonus.Id);
            Assert.That(QuestGraphValidator.HasErrors(diagnostics), Is.False, string.Join("\n", diagnostics));
        }

        [Test]
        public void Warning_ObjectiveWithoutDescription()
        {
            quest.Kill.Description = string.Empty;

            var diagnostics = Validate();

            AssertHasWarning(diagnostics, QuestDiagnosticCodes.ObjectiveWithoutDescription, quest.Kill.Id);
            Assert.That(QuestGraphValidator.HasErrors(diagnostics), Is.False);
        }

        [Test]
        public void Warning_CompletedEndWithoutRewards()
        {
            quest.Graph.RemoveNode(quest.Gold.Id);
            quest.Graph.RemoveNode(quest.Experience.Id);

            var diagnostics = Validate();

            AssertHasWarning(diagnostics, QuestDiagnosticCodes.CompletedEndWithoutRewards, quest.Completed.Id);
            Assert.That(QuestGraphValidator.HasErrors(diagnostics), Is.False);
        }

        [Test]
        public void Diagnostics_AreSortedErrorsFirst()
        {
            quest.Kill.Description = string.Empty;
            quest.Graph.QuestId = string.Empty;

            var severities = Validate().Select(d => d.Severity).ToList();

            Assert.That(severities, Is.EqualTo(severities.OrderByDescending(s => s).ToList()));
            Assert.That(severities.First(), Is.EqualTo(QuestDiagnosticSeverity.Error));
        }

        [Test]
        public void HasErrors_IgnoresWarnings()
        {
            Assert.That(QuestGraphValidator.HasErrors(null), Is.False);
            Assert.That(QuestGraphValidator.HasErrors(new[]
            {
                new QuestDiagnostic(QuestDiagnosticSeverity.Warning, "QW", "w")
            }), Is.False);
            Assert.That(QuestGraphValidator.HasErrors(new[]
            {
                new QuestDiagnostic(QuestDiagnosticSeverity.Error, "QE", "e")
            }), Is.True);
        }

        private List<QuestDiagnostic> Validate()
        {
            return QuestGraphValidator.Validate(quest.Graph);
        }

        private static void AssertHasError(List<QuestDiagnostic> diagnostics, string code, string nodeId = null)
        {
            AssertHas(diagnostics, QuestDiagnosticSeverity.Error, code, nodeId);
        }

        private static void AssertHasWarning(List<QuestDiagnostic> diagnostics, string code, string nodeId = null)
        {
            AssertHas(diagnostics, QuestDiagnosticSeverity.Warning, code, nodeId);
        }

        private static void AssertHas(List<QuestDiagnostic> diagnostics, QuestDiagnosticSeverity severity, string code, string nodeId)
        {
            var found = diagnostics.Any(d => d.Severity == severity && d.Code == code && (nodeId == null || d.NodeId == nodeId));
            Assert.That(found, Is.True, $"Expected {severity} {code} on node {nodeId ?? "<any>"}. Actual:\n{string.Join("\n", diagnostics)}");
        }

        private static void AssertHasNo(List<QuestDiagnostic> diagnostics, string code)
        {
            Assert.That(diagnostics.Any(d => d.Code == code), Is.False, string.Join("\n", diagnostics));
        }
    }
}
