using System.Collections.Generic;
using UnityEngine;

namespace Vokazhira.QuestEditor.Samples.NpcDelivery
{
    /// <summary>
    /// Отслеживает NPC рядом с игроком (через trigger-коллайдеры NPC) и по клавише E начинает разговор.
    /// </summary>
    public sealed class PlayerInteractor : MonoBehaviour
    {
        [SerializeField, Tooltip("Key used to talk to NPCs.")]
        private KeyCode interactKey = KeyCode.E;

        [SerializeField, Tooltip("HUD that shows dialogue lines.")]
        private NpcDeliveryHud hud;

        private readonly List<Npc> nearbyNpcs = new List<Npc>();

        /// <summary>
        /// Ближайший NPC в зоне разговора или null.
        /// </summary>
        public Npc Current
        {
            get
            {
                Npc nearest = null;
                var nearestDistance = float.MaxValue;
                foreach (var npc in nearbyNpcs)
                {
                    if (npc == null)
                    {
                        continue;
                    }

                    var distance = (npc.transform.position - transform.position).sqrMagnitude;
                    if (distance < nearestDistance)
                    {
                        nearest = npc;
                        nearestDistance = distance;
                    }
                }

                return nearest;
            }
        }

        public KeyCode InteractKey => interactKey;

        private void Update()
        {
            var npc = Current;
            if (npc != null && Input.GetKeyDown(interactKey) && hud != null)
            {
                hud.ShowDialogue(npc.DisplayName, npc.Interact());
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            var npc = other.GetComponentInParent<Npc>();
            if (npc != null && !nearbyNpcs.Contains(npc))
            {
                nearbyNpcs.Add(npc);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            var npc = other.GetComponentInParent<Npc>();
            if (npc != null)
            {
                nearbyNpcs.Remove(npc);
            }
        }
    }
}
