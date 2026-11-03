using UnityEngine;

namespace Vokazhira.QuestEditor.Samples.NpcDelivery
{
    /// <summary>
    /// Движение игрока на WASD / стрелках (старый Input Manager) и камера, которая следует сверху-сзади.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerController : MonoBehaviour
    {
        [SerializeField, Tooltip("Movement speed in units per second.")]
        private float speed = 6f;

        [SerializeField, Tooltip("Camera that follows the player.")]
        private Transform cameraTransform;

        [SerializeField, Tooltip("Camera offset from the player.")]
        private Vector3 cameraOffset = new Vector3(0f, 9f, -9f);

        private CharacterController characterController;

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
        }

        private void Update()
        {
            var input = new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical"));
            var direction = Vector3.ClampMagnitude(input, 1f);
            characterController.SimpleMove(direction * speed);

            if (direction.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), 12f * Time.deltaTime);
            }
        }

        private void LateUpdate()
        {
            if (cameraTransform == null)
            {
                return;
            }

            cameraTransform.position = transform.position + cameraOffset;
            cameraTransform.LookAt(transform.position + Vector3.up);
        }
    }
}
