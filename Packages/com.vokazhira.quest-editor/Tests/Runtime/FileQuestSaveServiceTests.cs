using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Vokazhira.QuestEditor.Tests
{
    public sealed class FileQuestSaveServiceTests
    {
        private string directory;
        private string filePath;
        private FileQuestSaveService service;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "QuestEditorTests", Guid.NewGuid().ToString("N"));
            filePath = Path.Combine(directory, "quests.json");
            service = new FileQuestSaveService(filePath);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }

        [Test]
        public void MissingFile_LoadsEmptyStateWithoutError()
        {
            Assert.That(service.TryLoad(out var data, out var error), Is.True, error);

            Assert.That(error, Is.Null);
            Assert.That(data.saveVersion, Is.EqualTo(QuestSaveData.CurrentVersion));
            Assert.That(data.quests, Is.Empty);
            Assert.That(data.orphanedQuests, Is.Empty);
        }

        [Test]
        public void SaveThenLoad_RoundTripsAllFields()
        {
            var saved = new QuestSaveData();
            var record = new QuestInstanceSaveData { questId = "forest", state = "Claimable", currentNodeId = "end-1" };
            record.progress.Add(new ObjectiveProgressEntry("kill-goblins", 7));
            record.grantedRewardIds.Add("gold-1");
            saved.quests.Add(record);
            saved.orphanedQuests.Add(new QuestInstanceSaveData { questId = "ghost", state = "Active", currentNodeId = "n" });

            service.Save(saved);
            Assert.That(service.TryLoad(out var loaded, out var error), Is.True, error);

            Assert.That(loaded.saveVersion, Is.EqualTo(QuestSaveData.CurrentVersion));
            Assert.That(loaded.quests.Count, Is.EqualTo(1));
            var quest = loaded.quests[0];
            Assert.That(quest.questId, Is.EqualTo("forest"));
            Assert.That(quest.state, Is.EqualTo("Claimable"));
            Assert.That(quest.currentNodeId, Is.EqualTo("end-1"));
            Assert.That(quest.progress[0].objectiveId, Is.EqualTo("kill-goblins"));
            Assert.That(quest.progress[0].value, Is.EqualTo(7));
            Assert.That(quest.grantedRewardIds, Is.EqualTo(new[] { "gold-1" }));
            Assert.That(loaded.orphanedQuests[0].questId, Is.EqualTo("ghost"));
        }

        [Test]
        public void Save_OverwritesExistingFileAndLeavesNoTempFile()
        {
            var first = new QuestSaveData();
            first.quests.Add(new QuestInstanceSaveData { questId = "a", state = "Active" });
            service.Save(first);

            var second = new QuestSaveData();
            second.quests.Add(new QuestInstanceSaveData { questId = "b", state = "Completed" });
            service.Save(second);

            Assert.That(File.Exists(filePath + FileQuestSaveService.TempSuffix), Is.False);
            Assert.That(service.TryLoad(out var loaded, out _), Is.True);
            Assert.That(loaded.quests.Count, Is.EqualTo(1));
            Assert.That(loaded.quests[0].questId, Is.EqualTo("b"));
        }

        [Test]
        public void CorruptedJson_ReturnsErrorAndKeepsCorruptCopy()
        {
            Directory.CreateDirectory(directory);
            const string garbage = "{ \"saveVersion\": 1, \"quests\": [ {";
            File.WriteAllText(filePath, garbage);

            Assert.That(service.TryLoad(out var data, out var error), Is.False);

            Assert.That(data, Is.Null);
            Assert.That(error, Does.Contain("повреждено"));
            Assert.That(File.ReadAllText(filePath + FileQuestSaveService.CorruptSuffix), Is.EqualTo(garbage));
        }

        [Test]
        public void EmptyFile_IsTreatedAsCorrupted()
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(filePath, "   ");

            Assert.That(service.TryLoad(out _, out var error), Is.False);
            Assert.That(error, Does.Contain("пуст"));
            Assert.That(File.Exists(filePath + FileQuestSaveService.CorruptSuffix), Is.True);
        }

        [Test]
        public void SaveAfterCorruption_WritesValidFile()
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(filePath, "not json");
            service.TryLoad(out _, out _);

            service.Save(new QuestSaveData());

            Assert.That(service.TryLoad(out var data, out var error), Is.True, error);
            Assert.That(data.quests, Is.Empty);
        }

        [Test]
        public void MissingListsInJson_AreNormalizedToEmpty()
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(filePath, "{\"saveVersion\":1,\"quests\":[{\"questId\":\"a\",\"state\":\"Active\"}]}");

            Assert.That(service.TryLoad(out var data, out var error), Is.True, error);

            Assert.That(data.orphanedQuests, Is.Not.Null);
            Assert.That(data.quests[0].progress, Is.Not.Null);
            Assert.That(data.quests[0].grantedRewardIds, Is.Not.Null);
        }

        [Test]
        public void RelativePath_ResolvesUnderPersistentDataPath()
        {
            var relative = new FileQuestSaveService("saves/progress.json");
            var byDefault = new FileQuestSaveService();

            Assert.That(relative.FilePath, Is.EqualTo(Path.Combine(Application.persistentDataPath, "saves/progress.json")));
            Assert.That(byDefault.FilePath, Is.EqualTo(Path.Combine(Application.persistentDataPath, FileQuestSaveService.DefaultFileName)));
        }

        [Test]
        public void InvalidArguments_Throw()
        {
            Assert.Throws<ArgumentException>(() => new FileQuestSaveService(" "));
            Assert.Throws<ArgumentNullException>(() => service.Save(null));
        }
    }
}
