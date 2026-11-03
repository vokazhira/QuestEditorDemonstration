using UnityEngine;

namespace Vokazhira.QuestEditor.Samples.NpcDelivery
{
    /// <summary>
    /// Общая часть NPC: id, имя, цвет и доступ к игре. Конкретное поведение — в наследниках.
    /// Рядом должен быть trigger-коллайдер: по нему игрок понимает, что может поговорить.
    /// </summary>
    public abstract class Npc : MonoBehaviour
    {
        [SerializeField, Tooltip("NPC id used in quest events, for example blacksmith.")]
        private string npcId = string.Empty;

        [SerializeField, Tooltip("Name shown above the NPC.")]
        private string displayName = string.Empty;

        [SerializeField, Tooltip("Body color.")]
        private Color color = Color.white;

        [SerializeField, Tooltip("Bootstrap that owns the QuestManager.")]
        private NpcDeliveryBootstrap bootstrap;

        public string NpcId => npcId;
        public string DisplayName => displayName;
        protected NpcDeliveryBootstrap Bootstrap => bootstrap;

        /// <summary>
        /// Текст подсказки «[E] …», когда игрок рядом.
        /// </summary>
        public abstract string Prompt { get; }

        /// <summary>
        /// Разговор с NPC. Возвращает реплику для окна диалога.
        /// </summary>
        public abstract string Interact();

        protected virtual void Awake()
        {
            var bodyRenderer = GetComponent<Renderer>();
            if (bodyRenderer != null)
            {
                bodyRenderer.material.color = color;
            }
        }
    }
}
