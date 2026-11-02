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

        [SerializeField, Tooltip("Bootstrap, создающий QuestManager.")]
        private QuestDemoBootstrap bootstrap;

        private readonly List<string> messages = new List<string>();
        private QuestManager subscribedManager;
        private Vector2 scroll;

        private void Start()
        {
            if (bootstrap == null || bootstrap.Manager == null)
            {
                Debug.LogError("QuestDemoUI: не назначен bootstrap или менеджер не создан.", this);
                enabled = false;
                return;
            }

            subscribedManager = bootstrap.Manager;
            subscribedManager.QuestStarted += q => AddMessage($"Задание начато: {Title(q)}");
            subscribedManager.QuestClaimable += q => AddMessage($"Можно забрать награду: {Title(q)}");
            subscribedManager.QuestCompleted += q => AddMessage($"Задание выполнено: {Title(q)}");
            subscribedManager.QuestFailed += q => AddMessage($"Задание провалено: {Title(q)}");
            subscribedManager.QuestRewardGranted += (q, r) => AddMessage($"Награда: {r.GetSummary()}");
            subscribedManager.QuestRewardFailed += (q, r, reason) => AddMessage($"Награда не выдана: {r.GetSummary()} ({reason})");
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

            GUILayout.Label($"Золото: {bootstrap.Currency.Gold}    Опыт: {bootstrap.Experience.Experience}");

            GUILayout.Space(6);
            GUILayout.Label("Действия игрока");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Убить гоблина"))
            {
                manager.Report(QuestEvent.EnemyKilled("goblin"));
            }

            if (GUILayout.Button("Найти письмо"))
            {
                manager.Report(QuestEvent.ItemCollected("letter"));
            }

            GUILayout.EndHorizontal();

            GUILayout.Space(6);
            GUILayout.Label("Задания");
            foreach (var quest in manager.GetInstances())
            {
                DrawQuest(manager, quest);
            }

            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Save"))
            {
                AddMessage(manager.Save() ? "Прогресс сохранён." : "Не удалось сохранить прогресс (см. Console).");
            }

            if (GUILayout.Button("Load"))
            {
                AddMessage(manager.Load() ? "Прогресс загружен." : "Не удалось загрузить прогресс (см. Console).");
            }

            if (GUILayout.Button("Сбросить прогресс"))
            {
                manager.Restore(new QuestSaveData());
                AddMessage("Прогресс сброшен (файл не изменён — нажмите Save, чтобы перезаписать).");
            }

            GUILayout.EndHorizontal();
            GUILayout.Label($"Файл: {bootstrap.SaveService.FilePath}");

            GUILayout.Space(6);
            GUILayout.Label("Сообщения");
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
            GUILayout.Label($"{Title(quest)} — {quest.State}{(quest.IsFrozen ? " (заморожено)" : string.Empty)}");

            var objective = quest.CurrentObjective;
            if (objective != null)
            {
                var name = string.IsNullOrEmpty(objective.Title) ? objective.GetSummary() : objective.Title;
                GUILayout.Label($"Цель: {name} — {quest.GetProgress(objective.ObjectiveId)}/{objective.RequiredCount}");
            }

            var errors = manager.GetValidationErrors(quest.QuestId);
            if (errors.Count > 0)
            {
                GUILayout.Label($"Задание невалидно: {errors[0].Message}");
            }

            GUILayout.BeginHorizontal();
            if (manager.CanStart(quest.QuestId) && GUILayout.Button("Начать"))
            {
                manager.Start(quest.QuestId);
            }

            if (quest.State == QuestState.Claimable && GUILayout.Button("Забрать награду"))
            {
                manager.Claim(quest.QuestId);
            }

            if (quest.State == QuestState.Active && !quest.IsFrozen && GUILayout.Button("Отказаться"))
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
