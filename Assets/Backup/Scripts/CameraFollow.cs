using UnityEngine;
using UnityEngine.InputSystem;

namespace JayFos.Runtime
{
    [RequireComponent(typeof(Camera))]
    public class CameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 targetOffset = new Vector3(0f, 1.5f, 0f);
        [SerializeField] private float distance = 7f;
        [SerializeField] private float smoothSpeed = 10f;

        [Header("Rotation Settings")]
        [SerializeField] private float mouseSensitivity = 0.2f;
        [SerializeField] private float minVerticalAngle = -20f;
        [SerializeField] private float maxVerticalAngle = 80f;

        private float currentX = 0f;
        private float currentY = 20f;
        private Camera cachedCamera;

        private void Awake()
        {
            cachedCamera = GetComponent<Camera>();
            if (cachedCamera == null)
            {
                cachedCamera = gameObject.AddComponent<Camera>();
            }
        }

        private void Start()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void LateUpdate()
        {
            if (target == null) return;

            if (Mouse.current != null)
            {
                Vector2 mouseDelta = Mouse.current.delta.ReadValue();
                
                currentX += mouseDelta.x * mouseSensitivity;
                currentY -= mouseDelta.y * mouseSensitivity;

                currentY = Mathf.Clamp(currentY, minVerticalAngle, maxVerticalAngle);
            }

            Quaternion rotation = Quaternion.Euler(currentY, currentX, 0f);
            Vector3 direction = new Vector3(0f, 0f, -distance);
            
            Vector3 targetLookAtPoint = target.position + targetOffset;
            Vector3 desiredPosition = targetLookAtPoint + (rotation * direction);

            transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);
            transform.LookAt(targetLookAtPoint);
        }
    }
}
