using System.Linq;
using NUnit.Framework;
using UnityEditor.Experimental.GraphView;

namespace Vokazhira.QuestEditor.Editor.Tests
{
    public sealed class QuestNodeTypeCacheTests
    {
        [Test]
        public void CreatableTypes_ContainAllBuiltInNodes()
        {
            var types = QuestNodeTypeCache.CreatableTypes.Select(t => t.Type).ToList();

            Assert.That(types, Is.SupersetOf(new[]
            {
                typeof(StartNodeData), typeof(EndNodeData),
                typeof(KillObjective), typeof(CollectObjective), typeof(CompleteWaveObjective),
                typeof(QuestCompletedCondition),
                typeof(GoldReward), typeof(ExperienceReward)
            }));
        }

        [Test]
        public void AbstractBaseTypes_AreNotCreatable()
        {
            Assert.That(QuestNodeTypeCache.IsCreatable(typeof(QuestNodeData)), Is.False);
            Assert.That(QuestNodeTypeCache.IsCreatable(typeof(ObjectiveNodeData)), Is.False);
            Assert.That(QuestNodeTypeCache.IsCreatable(typeof(ConditionNodeData)), Is.False);
            Assert.That(QuestNodeTypeCache.IsCreatable(typeof(RewardNodeData)), Is.False);
            Assert.That(QuestNodeTypeCache.IsCreatable(typeof(string)), Is.False);
        }

        [Test]
        public void GetInfo_UsesAttributeMenuPathAndColor()
        {
            var info = QuestNodeTypeCache.GetInfo(typeof(KillObjective));

            Assert.That(info.MenuPath, Is.EqualTo("Цели/Убить врагов"));
            Assert.That(info.DisplayName, Is.EqualTo("Убить врагов"));
            Assert.That(info.Color, Is.Not.EqualTo(QuestNodeTypeInfo.DefaultColor));
        }

        [Test]
        public void BuildTree_GroupsEntriesByMenuPath()
        {
            var infos = new[]
            {
                new QuestNodeTypeInfo(typeof(GoldReward), new QuestNodeAttribute("B/Z")),
                new QuestNodeTypeInfo(typeof(StartNodeData), new QuestNodeAttribute("A/X")),
                new QuestNodeTypeInfo(typeof(EndNodeData), new QuestNodeAttribute("Top")),
                new QuestNodeTypeInfo(typeof(KillObjective), new QuestNodeAttribute("A/Y"))
            };

            var tree = QuestNodeSearchProvider.BuildTree(infos);

            var actual = tree.Select(e => $"{e.level}:{(e is SearchTreeGroupEntry ? "G" : "E")}:{e.name}").ToArray();
            Assert.That(actual, Is.EqualTo(new[]
            {
                "0:G:Создать узел",
                "1:G:A",
                "2:E:X",
                "2:E:Y",
                "1:G:B",
                "2:E:Z",
                "1:E:Top"
            }));
            Assert.That(tree.Last().userData, Is.EqualTo(typeof(EndNodeData)));
        }
    }
}
