using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Vokazhira.QuestEditor.Editor.Tests
{
    public sealed class QuestEditorValidationTests
    {
        private const string TempFolder = "Assets/QuestEditorTestsTemp";

        private QuestWorkingCopy workingCopy;

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(TempFolder))
            {
                AssetDatabase.CreateFolder("Assets", "QuestEditorTestsTemp");
            }
        }

        [TearDown]
        public void TearDown()
        {
            workingCopy?.Dispose();
            workingCopy = null;
            if (AssetDatabase.IsValidFolder(TempFolder))
            {
                AssetDatabase.DeleteAsset(TempFolder);
            }
        }

        [Test]
        public void WorkingCopy_IsNotReportedAsDuplicateOfItsOwnAsset()
        {
            var asset = CreateQuestAsset("Own.asset", "unique_quest_id_for_tests");
            workingCopy = QuestWorkingCopy.Open(asset);

            var diagnostics = QuestEditorValidation.Validate(workingCopy.Graph, asset);

            Assert.That(diagnostics.Any(d => d.Code == QuestDiagnosticCodes.DuplicateQuestId), Is.False, string.Join("\n", diagnostics));
        }

        [Test]
        public void AnotherAssetWithSameQuestId_IsReportedAsDuplicate()
        {
            var asset = CreateQuestAsset("First.asset", "shared_quest_id_for_tests");
            CreateQuestAsset("Second.asset", "shared_quest_id_for_tests");
            workingCopy = QuestWorkingCopy.Open(asset);

            var diagnostics = QuestEditorValidation.Validate(workingCopy.Graph, asset);

            Assert.That(diagnostics.Any(d => d.Code == QuestDiagnosticCodes.DuplicateQuestId), Is.True, string.Join("\n", diagnostics));
        }

        [Test]
        public void AssetWithoutMissingTypes_IsNotReportedAsMissing()
        {
            var asset = CreateQuestAsset("Clean.asset", "clean_quest_id_for_tests");

            Assert.That(QuestEditorValidation.HasMissingTypes(asset), Is.False);
            Assert.That(QuestEditorValidation.Validate(asset, asset).Any(d => d.Code == QuestDiagnosticCodes.MissingNodeType), Is.False);
        }

        private static QuestGraphAsset CreateQuestAsset(string fileName, string questId)
        {
            var asset = ScriptableObject.CreateInstance<QuestGraphAsset>();
            asset.QuestId = questId;
            QuestAssetUtility.PopulateDefaultGraph(asset);
            AssetDatabase.CreateAsset(asset, TempFolder + "/" + fileName);
            AssetDatabase.SaveAssets();
            return asset;
        }
    }
}
