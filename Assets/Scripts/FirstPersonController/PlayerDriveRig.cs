using UnityEngine;
using System.Reflection;

namespace JayFos.Runtime
{
    /// <summary>Temporary diagnostic driver: injects sustained input into PlayerInput
    /// AFTER its Update reads the real device, so FixedUpdate consumers see it.
    /// Not part of the shipped controller.</summary>
    [DefaultExecutionOrder(3000)]
    public sealed class PlayerDriveRig : MonoBehaviour
    {
        public bool forwardHeld;
        public bool backwardHeld;
        public bool leftHeld;
        public bool rightHeld;
        public bool sprintHeld;
        public bool jumpThisFrame;

        private PlayerInput input;
        private FieldInfo moveField;
        private FieldInfo sprintField;
        private FieldInfo jumpField;

        private void Awake()
        {
            input = GetComponent<PlayerInput>();
            System.Type t = typeof(PlayerInput);
            moveField = t.GetField("<Move>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
            sprintField = t.GetField("<SprintHeld>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
            jumpField = t.GetField("<JumpPressed>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
        }

        private void Update()
        {
            if (input == null) return;
            Vector2 m = Vector2.zero;
            if (forwardHeld) m.y += 1f;
            if (backwardHeld) m.y -= 1f;
            if (leftHeld) m.x -= 1f;
            if (rightHeld) m.x += 1f;
            moveField?.SetValue(input, m);
            sprintField?.SetValue(input, sprintHeld);
            jumpField?.SetValue(input, jumpThisFrame);
            jumpThisFrame = false;
        }

        public void StartScenario(Vector3 bottom, Vector2 startInput, float holdTime, int jumpAt)
        {
            StartCoroutine(Scenario(bottom, startInput, holdTime, jumpAt));
        }

        public void StartRepeatedJumps(float interval)
        {
            StartCoroutine(RepeatedJumps(interval));
        }

        private System.Collections.IEnumerator RepeatedJumps(float interval)
        {
            yield return new WaitForSeconds(0.5f);
            while (true)
            {
                jumpThisFrame = true;
                jumpThisFramePending = Time.fixedTime;
                yield return new WaitForSeconds(Mathf.Max(0.01f, interval));
            }
        }

        public float jumpThisFramePending; // for correlating the press with FixedUpdate

        private System.Collections.IEnumerator Scenario(Vector3 bottom, Vector2 start, float holdTime, int jump)
        {
            yield return null;
            transform.position = bottom;
            transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            yield return new WaitForSeconds(0.6f);
            forwardHeld = start.y > 0f;
            sprintHeld = start.x > 1f;
            for (float elapsed = 0f; elapsed < holdTime; elapsed += Time.unscaledDeltaTime)
            {
                if (jump > 0 && elapsed > jump)
                {
                    jumpThisFrame = true;
                    jump = -1;
                }
                yield return null;
            }
            forwardHeld = false;
            sprintHeld = false;
        }
    }
}
