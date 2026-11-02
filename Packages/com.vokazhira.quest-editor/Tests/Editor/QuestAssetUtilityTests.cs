using System.Collections.Generic;
using NUnit.Framework;

namespace Vokazhira.QuestEditor.Editor.Tests
{
    public sealed class QuestAssetUtilityTests
    {
        [TestCase("Forest Cleanup", "forest_cleanup")]
        [TestCase("  Intro--Quest 2 ", "intro_quest_2")]
        [TestCase("Зачистка леса", "зачистка_леса")]
        [TestCase("   ", "quest")]
        [TestCase(null, "quest")]
        [TestCase("!!!", "quest")]
        public void SuggestQuestId_NormalizesName(string name, string expected)
        {
            Assert.That(QuestAssetUtility.SuggestQuestId(name), Is.EqualTo(expected));
        }

        [Test]
        public void MakeUniqueQuestId_ReturnsDesiredWhenFree()
        {
            var existing = new HashSet<string> { "intro" };

            Assert.That(QuestAssetUtility.MakeUniqueQuestId("forest", existing), Is.EqualTo("forest"));
        }

        [Test]
        public void MakeUniqueQuestId_AddsFirstFreeSuffix()
        {
            var existing = new HashSet<string> { "forest", "forest_2" };

            Assert.That(QuestAssetUtility.MakeUniqueQuestId("forest", existing), Is.EqualTo("forest_3"));
        }

        [Test]
        public void MakeUniqueQuestId_EmptyDesiredFallsBackToDefault()
        {
            Assert.That(QuestAssetUtility.MakeUniqueQuestId(" ", new HashSet<string>()), Is.EqualTo(QuestAssetUtility.DefaultQuestId));
            Assert.That(QuestAssetUtility.MakeUniqueQuestId(null, null), Is.EqualTo(QuestAssetUtility.DefaultQuestId));
        }

        [Test]
        public void PopulateDefaultGraph_AddsStartAndCompletedEndOnlyToEmptyGraph()
        {
            var graph = UnityEngine.ScriptableObject.CreateInstance<QuestGraphAsset>();
            try
            {
                QuestAssetUtility.PopulateDefaultGraph(graph);
                Assert.That(graph.Nodes.Count, Is.EqualTo(2));
                Assert.That(graph.Nodes[0], Is.InstanceOf<StartNodeData>());
                Assert.That(((EndNodeData)graph.Nodes[1]).Result, Is.EqualTo(QuestEndResult.Completed));

                QuestAssetUtility.PopulateDefaultGraph(graph);
                Assert.That(graph.Nodes.Count, Is.EqualTo(2));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(graph);
            }
        }
    }
}
