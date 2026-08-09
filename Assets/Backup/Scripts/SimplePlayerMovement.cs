using UnityEngine;
using UnityEngine.InputSystem;

namespace JayFos.Runtime
{
    [RequireComponent(typeof(Rigidbody))]
    public class SimplePlayerMovement : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float jumpForce = 5f;

        private Rigidbody rb;
        private Camera cachedCamera;
        private Animator animator;
        private bool isGrounded;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            cachedCamera = Camera.main;
            animator = GetComponentInChildren<Animator>();
        }

        private void Update()
        {
            if (Keyboard.current == null) return;

            float h = 0f;
            float v = 0f;

            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) v = 1f;
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) v = -1f;
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) h = -1f;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) h = 1f;

            if (cachedCamera == null) return;

            Vector3 camForward = cachedCamera.transform.forward;
            Vector3 camRight = cachedCamera.transform.right;
            
            camForward.y = 0f;
            camRight.y = 0f;
            camForward.Normalize();
            camRight.Normalize();

            Vector3 moveDirection = (camForward * v + camRight * h).normalized;
            Vector3 move = moveDirection * moveSpeed;

            rb.linearVelocity = new Vector3(move.x, rb.linearVelocity.y, move.z);

            if (Keyboard.current.spaceKey.wasPressedThisFrame && isGrounded)
            {
                rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
                animator.SetTrigger("Jump");
            }

            if (animator != null)
            {
                animator.SetFloat("Speed", new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z).magnitude, 0.1f, Time.deltaTime);
                animator.SetBool("Grounded", isGrounded);
            }
        }

        private void OnCollisionStay(Collision collision)
        {
            isGrounded = true;
        }

        private void OnCollisionExit(Collision collision)
        {
            isGrounded = false;
        }
    }
}
