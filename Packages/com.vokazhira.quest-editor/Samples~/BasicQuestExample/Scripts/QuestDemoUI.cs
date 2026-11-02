using System.Collections.Generic;
using UnityEngine;

namespace Vokazhira.QuestEditor.Demo
{
    /// <summary>
    /// Простой интерфейс демо на IMGUI: игровые действия, задания с прогрессом, золото и опыт,
    /// сообщения о событиях, Save / Load. Не требует дополнительных ассетов.
    /// </summary>
    public sealed class QuestDemoUI : MonoBehaviour
    {
        private const int MaxMessages = 8;

        [SerializeField, Tooltip("Bootstrap that creates the QuestManager.")]
        private QuestDemoBootstrap bootstrap;

        private readonly List<string> messages = new List<string>();
        private QuestManager subscribedManager;
        private Vector2 scroll;

        private void Start()
        {
            if (bootstrap == null || bootstrap.Manager == null)
            {
                Debug.LogError("QuestDemoUI: no bootstrap is assigned or the manager was not created.", this);
                enabled = false;
                return;
            }

            subscribedManager = bootstrap.Manager;
            subscribedManager.QuestStarted += q => AddMessage($"Quest started: {Title(q)}");
            subscribedManager.QuestClaimable += q => AddMessage($"Reward ready to claim: {Title(q)}");
            subscribedManager.QuestCompleted += q => AddMessage($"Quest completed: {Title(q)}");
            subscribedManager.QuestFailed += q => AddMessage($"Quest failed: {Title(q)}");
            subscribedManager.QuestRewardGranted += (q, r) => AddMessage($"Reward: {r.GetSummary()}");
            subscribedManager.QuestRewardFailed += (q, r, reason) => AddMessage($"Reward not granted: {r.GetSummary()} ({reason})");
        }

        private void OnGUI()
        {
            var manager = bootstrap != null ? bootstrap.Manager : null;
            if (manager == null)
            {
                return;
            }

            GUILayout.BeginArea(new Rect(10, 10, Mathf.Min(520, Screen.width - 20), Screen.height - 20), GUI.skin.box);
            scroll = GUILayout.BeginScrollView(scroll);

            GUILayout.Label($"Gold: {bootstrap.Currency.Gold}    XP: {bootstrap.Experience.Experience}");

            GUILayout.Space(6);
            GUILayout.Label("Player actions");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Kill goblin"))
            {
                manager.Report(QuestEvent.EnemyKilled("goblin"));
            }

            if (GUILayout.Button("Find letter"))
            {
                manager.Report(QuestEvent.ItemCollected("letter"));
            }

            GUILayout.EndHorizontal();

            GUILayout.Space(6);
            GUILayout.Label("Quests");
            foreach (var quest in manager.GetInstances())
            {
                DrawQuest(manager, quest);
            }

            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Save"))
            {
                AddMessage(manager.Save() ? "Progress saved." : "Could not save progress (see Console).");
            }

            if (GUILayout.Button("Load"))
            {
                AddMessage(manager.Load() ? "Progress loaded." : "Could not load progress (see Console).");
            }

            if (GUILayout.Button("Reset progress"))
            {
                manager.Restore(new QuestSaveData());
                AddMessage("Progress reset (the file is unchanged; press Save to overwrite it).");
            }

            GUILayout.EndHorizontal();
            GUILayout.Label($"File: {bootstrap.SaveService.FilePath}");

            GUILayout.Space(6);
            GUILayout.Label("Messages");
            foreach (var message in messages)
            {
                GUILayout.Label("• " + message);
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawQuest(QuestManager manager, QuestInstance quest)
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label($"{Title(quest)} — {quest.State}{(quest.IsFrozen ? " (frozen)" : string.Empty)}");

            var objective = quest.CurrentObjective;
            if (objective != null)
            {
                var name = string.IsNullOrEmpty(objective.Title) ? objective.GetSummary() : objective.Title;
                GUILayout.Label($"Objective: {name} — {quest.GetProgress(objective.ObjectiveId)}/{objective.RequiredCount}");
            }

            var errors = manager.GetValidationErrors(quest.QuestId);
            if (errors.Count > 0)
            {
                GUILayout.Label($"Quest is invalid: {errors[0].Message}");
            }

            GUILayout.BeginHorizontal();
            if (manager.CanStart(quest.QuestId) && GUILayout.Button("Start"))
            {
                manager.Start(quest.QuestId);
            }

            if (quest.State == QuestState.Claimable && GUILayout.Button("Claim reward"))
            {
                manager.Claim(quest.QuestId);
            }

            if (quest.State == QuestState.Active && !quest.IsFrozen && GUILayout.Button("Abandon"))
            {
                manager.Abandon(quest.QuestId);
            }

            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        private void AddMessage(string message)
        {
            messages.Insert(0, message);
            if (messages.Count > MaxMessages)
            {
                messages.RemoveAt(messages.Count - 1);
            }
        }

        private static string Title(QuestInstance quest)
        {
            return string.IsNullOrEmpty(quest.Graph.DisplayName) ? quest.QuestId : quest.Graph.DisplayName;
        }
    }
}
