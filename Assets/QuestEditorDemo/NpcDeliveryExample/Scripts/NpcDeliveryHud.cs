using System.Collections.Generic;
using UnityEngine;

namespace Vokazhira.QuestEditor.Samples.NpcDelivery
{
    /// <summary>
    /// Интерфейс сцены на IMGUI: золото, инвентарь, журнал заданий, подсказка «[E] …», реплики NPC,
    /// сообщения о событиях заданий и имена над NPC.
    /// </summary>
    public sealed class NpcDeliveryHud : MonoBehaviour
    {
        private const float DialogueSeconds = 6f;
        private const int MaxMessages = 5;

        [SerializeField, Tooltip("Bootstrap that owns the QuestManager.")]
        private NpcDeliveryBootstrap bootstrap;

        [SerializeField, Tooltip("Player interactor used for the talk prompt.")]
        private PlayerInteractor interactor;

        [SerializeField, Tooltip("Camera used to place NPC name labels.")]
        private Camera worldCamera;

        [SerializeField, Tooltip("NPCs that get a name label.")]
        private Npc[] npcs = new Npc[0];

        private readonly List<string> messages = new List<string>();
        private string dialogueSpeaker;
        private string dialogueLine;
        private float dialogueHideTime;
        private GUIStyle labelStyle;
        private GUIStyle boxStyle;

        public void ShowDialogue(string speaker, string line)
        {
            dialogueSpeaker = speaker;
            dialogueLine = line;
            dialogueHideTime = Time.time + DialogueSeconds;
        }

        private void Start()
        {
            var quests = bootstrap != null ? bootstrap.Quests : null;
            if (quests == null)
            {
                Debug.LogError("NpcDeliveryHud: no bootstrap is assigned or the QuestManager was not created.", this);
                enabled = false;
                return;
            }

            quests.QuestStarted += q => AddMessage($"Quest started: {Title(q)}");
            quests.QuestClaimable += q => AddMessage($"Objective complete. Return to claim the reward: {Title(q)}");
            quests.QuestRewardGranted += (q, r) => AddMessage($"Reward: {r.GetSummary()}");
            quests.QuestCompleted += q => AddMessage($"Quest completed: {Title(q)}");
        }

        private void OnGUI()
        {
            var quests = bootstrap != null ? bootstrap.Quests : null;
            if (quests == null)
            {
                return;
            }

            EnsureStyles();
            DrawNameLabels();

            GUILayout.BeginArea(new Rect(10, 10, 380, Screen.height - 20));
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("WASD / arrows - move, E - talk");
            GUILayout.Label($"Gold: {bootstrap.Wallet.Gold}");
            GUILayout.Label("Inventory: " + (bootstrap.Inventory.Items.Count == 0 ? "empty" : string.Join(", ", bootstrap.Inventory.Items)));
            GUILayout.EndVertical();

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("Quest log");
            var any = false;
            foreach (var quest in quests.GetInstances())
            {
                if (quest.State == QuestState.Available)
                {
                    continue;
                }

                any = true;
                GUILayout.Label($"{Title(quest)} - {DescribeQuest(quest)}");
            }

            if (!any)
            {
                GUILayout.Label("No quests yet. Talk to the Elder.");
            }

            GUILayout.EndVertical();

            foreach (var message in messages)
            {
                GUILayout.Label(message);
            }

            GUILayout.EndArea();

            var npc = interactor != null ? interactor.Current : null;
            if (npc != null)
            {
                var prompt = $"[{interactor.InteractKey}] {npc.Prompt}";
                GUI.Label(new Rect(Screen.width / 2f - 200, Screen.height - 150, 400, 30), prompt, labelStyle);
            }

            if (dialogueLine != null && Time.time < dialogueHideTime)
            {
                GUI.Box(new Rect(Screen.width / 2f - 300, Screen.height - 110, 600, 80), $"{dialogueSpeaker}: {dialogueLine}", boxStyle);
            }
        }

        private void DrawNameLabels()
        {
            if (worldCamera == null)
            {
                return;
            }

            foreach (var npc in npcs)
            {
                if (npc == null)
                {
                    continue;
                }

                var screen = worldCamera.WorldToScreenPoint(npc.transform.position + Vector3.up * 1.6f);
                if (screen.z <= 0f)
                {
                    continue;
                }

                GUI.Label(new Rect(screen.x - 100, Screen.height - screen.y - 12, 200, 24), npc.DisplayName, labelStyle);
            }
        }

        private static string DescribeQuest(QuestInstance quest)
        {
            switch (quest.State)
            {
                case QuestState.Active:
                    var objective = quest.CurrentObjective;
                    if (objective == null)
                    {
                        return "in progress";
                    }

                    var name = string.IsNullOrEmpty(objective.Title) ? objective.GetSummary() : objective.Title;
                    return $"{name} ({quest.GetProgress(objective.ObjectiveId)}/{objective.RequiredCount})";
                case QuestState.Claimable:
                    return "return to the quest giver for your reward";
                default:
                    return quest.State.ToString();
            }
        }

        private void AddMessage(string message)
        {
            messages.Insert(0, message);
            if (messages.Count > MaxMessages)
            {
                messages.RemoveAt(messages.Count - 1);
            }
        }

        private void EnsureStyles()
        {
            if (labelStyle != null)
            {
                return;
            }

            labelStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 16, fontStyle = FontStyle.Bold };
            boxStyle = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.MiddleCenter, fontSize = 16, wordWrap = true };
        }

        private static string Title(QuestInstance quest)
        {
            return string.IsNullOrEmpty(quest.Graph.DisplayName) ? quest.QuestId : quest.Graph.DisplayName;
        }
    }
}
