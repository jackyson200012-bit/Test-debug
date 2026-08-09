using UnityEngine;

namespace JayFos.Runtime.Testing
{
    /// <summary>
    /// Test helper: moves a kinematic Rigidbody as an elevator, conveyor or
    /// rotating platform to verify PlayerMotor's platform velocity inheritance.
    /// Not part of the shipped controller.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class PlatformTestMover : MonoBehaviour
    {
        public enum Kind { Elevator, Conveyor, Rotating }

        [SerializeField] private Kind kind = Kind.Elevator;
        [SerializeField] private Vector3 direction = Vector3.up;
        [SerializeField] private float speed = 1.5f;
        [SerializeField] private float range = 4f;
        [SerializeField] private Vector3 rotationAxis = Vector3.up;
        [SerializeField] private float rotationSpeed = 20f;

        private Rigidbody rb;
        private Vector3 start;
        private float t;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            start = rb.position;
        }

        private void FixedUpdate()
        {
            switch (kind)
            {
                case Kind.Elevator:
                case Kind.Conveyor:
                    t += speed * Time.fixedDeltaTime;
                    Vector3 offset = direction.normalized * (Mathf.PingPong(t, range) - range * 0.5f);
                    rb.MovePosition(start + offset);
                    break;
                case Kind.Rotating:
                    rb.MoveRotation(rb.rotation * Quaternion.AngleAxis(rotationSpeed * Time.fixedDeltaTime, rotationAxis));
                    break;
            }
        }
    }
}